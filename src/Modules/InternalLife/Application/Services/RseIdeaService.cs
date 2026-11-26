using Sirh.BuildingBlocks.Application.Abstractions;
using Sirh.BuildingBlocks.Application.Contracts;
using Sirh.BuildingBlocks.Application.Pagination;
using Sirh.BuildingBlocks.Application.Storage;
using Sirh.BuildingBlocks.Domain.Results;
using Sirh.Modules.InternalLife.Application.Abstractions;
using Sirh.Modules.InternalLife.Application.DTOs;
using Sirh.Modules.InternalLife.Domain.Constants;
using Sirh.Modules.InternalLife.Domain.Entities;
using Sirh.Modules.InternalLife.Domain.Enums;

namespace Sirh.Modules.InternalLife.Application.Services;

public sealed class RseIdeaService(
    IRseIdeaRepository ideaRepository,
    IOrganizationLookup organizationLookup,
    IFileValidator fileValidator,
    IFileStorageService fileStorageService,
    IDateTimeProvider dateTimeProvider,
    ICurrentUser currentUser) : IRseIdeaService
{
    private static readonly IReadOnlyDictionary<string, string> ExtensionMimeTypes =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [".pdf"] = "application/pdf",
            [".png"] = "image/png",
            [".jpg"] = "image/jpeg",
            [".jpeg"] = "image/jpeg",
            [".webp"] = "image/webp"
        };

    public async Task<Result<PagedResult<RseIdeaDto>>> GetPagedAsync(
        RseIdeaQuery query,
        PagedRequest pagedRequest,
        CancellationToken cancellationToken = default)
    {
        var canReadAll = HasPermission(InternalLifePermissions.RseReadAll);
        var canReadSelf = HasPermission(InternalLifePermissions.RseReadSelf);

        if (!canReadAll && !canReadSelf)
        {
            return Error.Forbidden("RseIdeas.Forbidden", "You do not have permission to view RSE ideas.");
        }

        var effectiveAuthorId = query.AuthorId;
        if (query.MineOnly == true || !canReadAll)
        {
            if (!currentUser.EmployeeId.HasValue)
            {
                return Error.Forbidden(
                    "RseIdeas.Forbidden",
                    "Your account must be linked to an employee profile to view your RSE ideas.");
            }

            if (!canReadAll && query.AuthorId.HasValue && query.AuthorId.Value != currentUser.EmployeeId.Value)
            {
                return Error.Forbidden("RseIdeas.Forbidden", "You can only view your own RSE ideas.");
            }

            effectiveAuthorId = currentUser.EmployeeId.Value;
        }

        var page = await ideaRepository.GetPagedAsync(
            effectiveAuthorId,
            query.LocationId,
            query.Status,
            query.Search,
            pagedRequest,
            cancellationToken);

        var mapped = page.Items.Select(MapToDto).ToArray();
        return PagedResult<RseIdeaDto>.Create(mapped, page.TotalCount, page.Page, page.PageSize);
    }

    public async Task<Result<RseIdeaDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var accessResult = await LoadAndAuthorizeReadAsync(id, cancellationToken);
        if (accessResult.IsFailure)
        {
            return accessResult.Error;
        }

        return MapToDto(accessResult.Value);
    }

    public async Task<Result<DownloadedCommonFileDto>> OpenAttachmentAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var accessResult = await LoadAndAuthorizeReadAsync(id, cancellationToken);
        if (accessResult.IsFailure)
        {
            return accessResult.Error;
        }

        var idea = accessResult.Value;
        if (string.IsNullOrWhiteSpace(idea.AttachmentFileKey) ||
            string.IsNullOrWhiteSpace(idea.AttachmentFileName) ||
            string.IsNullOrWhiteSpace(idea.AttachmentContentType) ||
            !idea.AttachmentFileSizeBytes.HasValue)
        {
            return Error.NotFound("RseIdeas.AttachmentNotFound", $"RSE idea '{id}' does not have an attachment.");
        }

        var stream = await fileStorageService.OpenReadAsync(
            StorageScope.EmployeeSensitive,
            idea.AttachmentFileKey,
            cancellationToken);

        return new DownloadedCommonFileDto(
            stream,
            idea.AttachmentFileName,
            idea.AttachmentContentType,
            idea.AttachmentFileSizeBytes.Value);
    }

    public async Task<Result<RseIdeaDto>> CreateAsync(
        CreateRseIdeaCommand command,
        CancellationToken cancellationToken = default)
    {
        if (!currentUser.EmployeeId.HasValue || currentUser.EmployeeId.Value == Guid.Empty)
        {
            return Error.Forbidden(
                "RseIdeas.Forbidden",
                "Your account must be linked to an employee profile to submit an RSE idea.");
        }

        if (string.IsNullOrWhiteSpace(command.Title) || string.IsNullOrWhiteSpace(command.IdeaContent))
        {
            return Error.Validation("RseIdeas.ContentRequired", "Title and IdeaContent are required.");
        }

        string? attachmentMime = null;
        if (command.Attachment is not null)
        {
            var fileValidation = await ValidateAttachmentAsync(command.Attachment, cancellationToken);
            if (fileValidation.IsFailure)
            {
                return fileValidation.Error;
            }

            attachmentMime = fileValidation.Value;
        }

        if (command.LocationId.HasValue &&
            !await organizationLookup.LocationExistsAsync(command.LocationId.Value, cancellationToken))
        {
            return Error.NotFound("Locations.NotFound", $"Location '{command.LocationId.Value}' was not found.");
        }

        var authorId = currentUser.EmployeeId.Value;
        var idea = new RseIdea(authorId, command.LocationId, command.Title, command.IdeaContent);

        if (command.Attachment is not null)
        {
            var stored = await fileStorageService.SaveAsync(
                StorageScope.EmployeeSensitive,
                $"employees/{authorId}/rse-ideas",
                command.Attachment.FileName,
                attachmentMime!,
                command.Attachment.Content,
                cancellationToken);

            idea.SetAttachment(stored.FileKey, stored.FileName, stored.ContentType, stored.SizeBytes);
        }

        await ideaRepository.AddAsync(idea, cancellationToken);
        await ideaRepository.SaveChangesAsync(cancellationToken);

        return MapToDto(idea);
    }

    public async Task<Result<RseIdeaDto>> UpdateAsync(
        Guid id,
        UpdateRseIdeaCommand command,
        CancellationToken cancellationToken = default)
    {
        var idea = await ideaRepository.GetByIdAsync(id, cancellationToken);
        if (idea is null)
        {
            return Error.NotFound("RseIdeas.NotFound", $"RSE idea '{id}' was not found.");
        }

        if (!currentUser.EmployeeId.HasValue || idea.AuthorEmployeeId != currentUser.EmployeeId.Value)
        {
            return Error.Forbidden("RseIdeas.Forbidden", "Only the author can modify this RSE idea.");
        }

        if (idea.Status != RseIdeaStatus.Submitted)
        {
            return Error.Conflict(
                "RseIdeas.NotEditable",
                $"RSE idea '{id}' is in status '{idea.Status}' and can no longer be edited.");
        }

        var rowVersionResult = InternalLifeConcurrencyHelper.ValidateRowVersion(command.RowVersion, idea.RowVersion);
        if (rowVersionResult.IsFailure)
        {
            return rowVersionResult.Error;
        }

        if (string.IsNullOrWhiteSpace(command.Title) || string.IsNullOrWhiteSpace(command.IdeaContent))
        {
            return Error.Validation("RseIdeas.ContentRequired", "Title and IdeaContent are required.");
        }

        string? attachmentMime = null;
        if (command.Attachment is not null)
        {
            var fileValidation = await ValidateAttachmentAsync(command.Attachment, cancellationToken);
            if (fileValidation.IsFailure)
            {
                return fileValidation.Error;
            }

            attachmentMime = fileValidation.Value;
        }

        if (command.LocationId.HasValue &&
            !await organizationLookup.LocationExistsAsync(command.LocationId.Value, cancellationToken))
        {
            return Error.NotFound("Locations.NotFound", $"Location '{command.LocationId.Value}' was not found.");
        }

        string? oldAttachmentKey = null;
        if (command.Attachment is not null)
        {
            var stored = await fileStorageService.SaveAsync(
                StorageScope.EmployeeSensitive,
                $"employees/{idea.AuthorEmployeeId}/rse-ideas",
                command.Attachment.FileName,
                attachmentMime!,
                command.Attachment.Content,
                cancellationToken);

            oldAttachmentKey = idea.AttachmentFileKey;
            idea.SetAttachment(stored.FileKey, stored.FileName, stored.ContentType, stored.SizeBytes);
        }

        idea.UpdateContent(command.LocationId, command.Title, command.IdeaContent);
        await ideaRepository.SaveChangesAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(oldAttachmentKey))
        {
            await fileStorageService.DeleteAsync(StorageScope.EmployeeSensitive, oldAttachmentKey, cancellationToken);
        }

        return MapToDto(idea);
    }

    public async Task<Result<RseIdeaDto>> TreatAsync(
        Guid id,
        TreatRseIdeaRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(request.Status) || request.Status == RseIdeaStatus.Submitted)
        {
            return Error.Validation(
                "RseIdeas.InvalidTransition",
                "Target status must be 'InReview', 'Treated', or 'Rejected'.");
        }

        var idea = await ideaRepository.GetByIdAsync(id, cancellationToken);
        if (idea is null)
        {
            return Error.NotFound("RseIdeas.NotFound", $"RSE idea '{id}' was not found.");
        }

        var rowVersionResult = InternalLifeConcurrencyHelper.ValidateRowVersion(request.RowVersion, idea.RowVersion);
        if (rowVersionResult.IsFailure)
        {
            return rowVersionResult.Error;
        }

        idea.UpdateStatus(request.Status, request.RhComment, currentUser.EmployeeId, dateTimeProvider.UtcNow);
        await ideaRepository.SaveChangesAsync(cancellationToken);

        return MapToDto(idea);
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var idea = await ideaRepository.GetByIdAsync(id, cancellationToken);
        if (idea is null)
        {
            return Result.Failure(Error.NotFound("RseIdeas.NotFound", $"RSE idea '{id}' was not found."));
        }

        var canTreat = HasPermission(InternalLifePermissions.RseTreat);
        if (!canTreat)
        {
            var isAuthor = HasPermission(InternalLifePermissions.RseWrite)
                           && currentUser.EmployeeId.HasValue
                           && currentUser.EmployeeId.Value == idea.AuthorEmployeeId;

            if (!isAuthor)
            {
                return Result.Failure(Error.Forbidden(
                    "RseIdeas.Forbidden",
                    "You do not have permission to delete this RSE idea."));
            }

            if (idea.Status != RseIdeaStatus.Submitted)
            {
                return Result.Failure(Error.Conflict(
                    "RseIdeas.NotDeletable",
                    $"RSE idea '{id}' is in status '{idea.Status}' and can no longer be deleted by its author."));
            }
        }

        var fileKey = idea.AttachmentFileKey;
        ideaRepository.Remove(idea);
        await ideaRepository.SaveChangesAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(fileKey))
        {
            await fileStorageService.DeleteAsync(StorageScope.EmployeeSensitive, fileKey, cancellationToken);
        }

        return Result.Success();
    }

    private async Task<Result<RseIdea>> LoadAndAuthorizeReadAsync(Guid id, CancellationToken cancellationToken)
    {
        var idea = await ideaRepository.GetByIdAsync(id, cancellationToken);
        if (idea is null)
        {
            return Error.NotFound("RseIdeas.NotFound", $"RSE idea '{id}' was not found.");
        }

        var canReadAll = HasPermission(InternalLifePermissions.RseReadAll);
        var canReadSelf = HasPermission(InternalLifePermissions.RseReadSelf);

        if (canReadAll ||
            (canReadSelf && currentUser.EmployeeId.HasValue && currentUser.EmployeeId.Value == idea.AuthorEmployeeId))
        {
            return idea;
        }

        return Error.Forbidden("RseIdeas.Forbidden", "You do not have permission to view this RSE idea.");
    }

    private bool HasPermission(string permission) =>
        currentUser.IsAuthenticated &&
        currentUser.Permissions.Contains(permission, StringComparer.OrdinalIgnoreCase);

    private async Task<Result<string>> ValidateAttachmentAsync(
        UploadedCommonFileInput file,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(file.FileName))
        {
            return Error.Validation("File.Required", "A valid attachment file is required.");
        }

        var extension = Path.GetExtension(file.FileName);
        if (!ExtensionMimeTypes.TryGetValue(extension, out var normalizedContentType))
        {
            return Error.Validation(
                "File.UnsupportedFormat",
                "Only PDF, PNG, JPEG and WEBP attachments are accepted.");
        }

        if (file.Content.CanSeek)
        {
            file.Content.Position = 0;
        }

        var validation = await fileValidator.ValidateAsync(
            file.Content,
            file.FileName,
            normalizedContentType,
            cancellationToken);

        if (file.Content.CanSeek)
        {
            file.Content.Position = 0;
        }

        if (!validation.IsValid)
        {
            return Error.Validation(
                "File.Invalid",
                validation.ErrorMessage ?? "The uploaded attachment content is invalid.");
        }

        return normalizedContentType;
    }

    private static RseIdeaDto MapToDto(RseIdea idea) =>
        new(
            idea.Id,
            idea.AuthorEmployeeId,
            idea.LocationId,
            idea.Title,
            idea.IdeaContent,
            !string.IsNullOrWhiteSpace(idea.AttachmentFileKey),
            idea.AttachmentFileName,
            idea.AttachmentContentType,
            idea.AttachmentFileSizeBytes,
            idea.Status,
            idea.RhComment,
            idea.TreatedByEmployeeId,
            idea.TreatedAtUtc,
            idea.CreatedAtUtc,
            idea.UpdatedAtUtc,
            Convert.ToBase64String(idea.RowVersion));
}

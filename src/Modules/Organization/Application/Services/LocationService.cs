using FluentValidation;
using Sirh.BuildingBlocks.Application.Contracts;
using Sirh.BuildingBlocks.Application.Pagination;
using Sirh.BuildingBlocks.Domain.Results;
using Sirh.Modules.Organization.Application.Abstractions;
using Sirh.Modules.Organization.Application.DTOs;
using Sirh.Modules.Organization.Domain.Entities;

namespace Sirh.Modules.Organization.Application.Services;

public sealed class LocationService(
    ILocationRepository locationRepository,
    ICompanyRepository companyRepository,
    IEnumerable<ILocationReferenceChecker> referenceCheckers,
    IValidator<CreateLocationRequest> createValidator,
    IValidator<UpdateLocationRequest> updateValidator) : ILocationService
{
    public async Task<Result<PagedResult<LocationDto>>> GetPagedAsync(
        bool? isPublished,
        Guid? companyId,
        string? search,
        PagedRequest pagedRequest,
        CancellationToken cancellationToken = default)
    {
        var paged = await locationRepository.GetPagedAsync(
            isPublished,
            companyId,
            search,
            pagedRequest,
            cancellationToken);

        var items = paged.Items.Select(ToDto).ToArray();
        return PagedResult<LocationDto>.Create(items, paged.TotalCount, paged.Page, paged.PageSize);
    }

    public async Task<Result<LocationDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var location = await locationRepository.GetByIdAsync(id, cancellationToken);
        return location is null
            ? Error.NotFound("Locations.NotFound", $"Location '{id}' was not found.")
            : ToDto(location);
    }

    public async Task<Result<LocationDto>> CreateAsync(
        CreateLocationRequest request,
        CancellationToken cancellationToken = default)
    {
        await createValidator.ValidateAndThrowAsync(request, cancellationToken);

        if (request.CompanyId.HasValue &&
            !await companyRepository.ExistsAsync(request.CompanyId.Value, cancellationToken))
        {
            return Error.NotFound("Companies.NotFound", $"Company '{request.CompanyId.Value}' was not found.");
        }

        var name = request.Name.Trim();
        var slug = await ConcurrencyHelper.GenerateUniqueSlugAsync(
            name,
            candidate => locationRepository.ExistsBySlugAsync(candidate, null, cancellationToken),
            "location");

        var location = new Location(
            slug,
            name,
            request.Address,
            request.PostalCode,
            request.City,
            request.Country,
            request.DepartmentCode,
            request.Siret,
            request.ColorCode,
            request.IsPublished,
            request.CompanyId);

        await locationRepository.AddAsync(location, cancellationToken);
        await locationRepository.SaveChangesAsync(cancellationToken);

        return ToDto(location);
    }

    public async Task<Result<LocationDto>> UpdateAsync(
        Guid id,
        UpdateLocationRequest request,
        CancellationToken cancellationToken = default)
    {
        await updateValidator.ValidateAndThrowAsync(request, cancellationToken);

        var location = await locationRepository.GetByIdAsync(id, cancellationToken);
        if (location is null)
        {
            return Error.NotFound("Locations.NotFound", $"Location '{id}' was not found.");
        }

        var concurrency = ConcurrencyHelper.ValidateRowVersion(request.RowVersion, location.RowVersion);
        if (concurrency.IsFailure)
        {
            return concurrency.Error;
        }

        if (request.CompanyId.HasValue &&
            !await companyRepository.ExistsAsync(request.CompanyId.Value, cancellationToken))
        {
            return Error.NotFound("Companies.NotFound", $"Company '{request.CompanyId.Value}' was not found.");
        }

        var name = request.Name.Trim();
        var slug = string.Equals(location.Name, name, StringComparison.OrdinalIgnoreCase)
            ? location.Slug
            : await ConcurrencyHelper.GenerateUniqueSlugAsync(
                name,
                candidate => locationRepository.ExistsBySlugAsync(candidate, id, cancellationToken),
                "location");

        location.Update(
            slug,
            name,
            request.Address,
            request.PostalCode,
            request.City,
            request.Country,
            request.DepartmentCode,
            request.Siret,
            request.ColorCode,
            request.IsPublished,
            request.CompanyId);

        await locationRepository.SaveChangesAsync(cancellationToken);
        return ToDto(location);
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var location = await locationRepository.GetByIdAsync(id, cancellationToken);
        if (location is null)
        {
            return Result.Failure(Error.NotFound("Locations.NotFound", $"Location '{id}' was not found."));
        }

        foreach (var checker in referenceCheckers)
        {
            if (await checker.IsLocationReferencedAsync(id, cancellationToken))
            {
                return Result.Failure(Error.Conflict(
                    "Locations.InUse",
                    $"Location '{id}' cannot be deleted because it is referenced by existing records."));
            }
        }

        locationRepository.Remove(location);
        await locationRepository.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private static LocationDto ToDto(Location l) =>
        new(
            l.Id,
            l.Slug,
            l.Name,
            l.Address,
            l.PostalCode,
            l.City,
            l.Country,
            l.DepartmentCode,
            l.Siret,
            l.ColorCode,
            l.IsPublished,
            l.CompanyId,
            l.CreatedAtUtc,
            l.UpdatedAtUtc,
            Convert.ToBase64String(l.RowVersion));
}

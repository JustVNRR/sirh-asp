using FluentValidation;
using Sirh.BuildingBlocks.Application.Pagination;
using Sirh.BuildingBlocks.Domain.Results;
using Sirh.Modules.Organization.Application.Abstractions;
using Sirh.Modules.Organization.Application.DTOs;
using Sirh.Modules.Organization.Domain.Entities;

namespace Sirh.Modules.Organization.Application.Services;

public sealed class CompanyService(
    ICompanyRepository companyRepository,
    IValidator<CreateCompanyRequest> createValidator,
    IValidator<UpdateCompanyRequest> updateValidator) : ICompanyService
{
    public async Task<Result<PagedResult<CompanyDto>>> GetPagedAsync(
        bool? isActive,
        string? search,
        PagedRequest pagedRequest,
        CancellationToken cancellationToken = default)
    {
        var paged = await companyRepository.GetPagedAsync(isActive, search, pagedRequest, cancellationToken);
        var items = paged.Items.Select(ToDto).ToArray();
        return PagedResult<CompanyDto>.Create(items, paged.TotalCount, paged.Page, paged.PageSize);
    }

    public async Task<Result<CompanyDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var company = await companyRepository.GetByIdAsync(id, cancellationToken);
        return company is null
            ? Error.NotFound("Companies.NotFound", $"Company '{id}' was not found.")
            : ToDto(company);
    }

    public async Task<Result<CompanyDto>> CreateAsync(
        CreateCompanyRequest request,
        CancellationToken cancellationToken = default)
    {
        await createValidator.ValidateAndThrowAsync(request, cancellationToken);

        var name = request.Name.Trim();
        var siret = request.Siret.Trim();

        if (await companyRepository.ExistsByNameAsync(name, null, cancellationToken))
        {
            return Error.Conflict("Companies.DuplicateName", $"A company named '{name}' already exists.");
        }

        if (await companyRepository.ExistsBySiretAsync(siret, null, cancellationToken))
        {
            return Error.Conflict("Companies.DuplicateSiret", $"A company with SIRET '{siret}' already exists.");
        }

        var slug = await ConcurrencyHelper.GenerateUniqueSlugAsync(
            name,
            candidate => companyRepository.ExistsBySlugAsync(candidate, null, cancellationToken),
            "company");

        var company = new Company(
            name,
            slug,
            request.LegalForm,
            request.Address,
            request.PostalCode,
            request.City,
            request.Country,
            siret,
            request.Register,
            request.Capital,
            request.CollectiveAgreement,
            request.Owner,
            request.LogoUrl);

        await companyRepository.AddAsync(company, cancellationToken);
        await companyRepository.SaveChangesAsync(cancellationToken);

        return ToDto(company);
    }

    public async Task<Result<CompanyDto>> UpdateAsync(
        Guid id,
        UpdateCompanyRequest request,
        CancellationToken cancellationToken = default)
    {
        await updateValidator.ValidateAndThrowAsync(request, cancellationToken);

        var company = await companyRepository.GetByIdAsync(id, cancellationToken);
        if (company is null)
        {
            return Error.NotFound("Companies.NotFound", $"Company '{id}' was not found.");
        }

        var concurrency = ConcurrencyHelper.ValidateRowVersion(request.RowVersion, company.RowVersion);
        if (concurrency.IsFailure)
        {
            return concurrency.Error;
        }

        var name = request.Name.Trim();
        var siret = request.Siret.Trim();

        if (await companyRepository.ExistsByNameAsync(name, id, cancellationToken))
        {
            return Error.Conflict("Companies.DuplicateName", $"A company named '{name}' already exists.");
        }

        if (await companyRepository.ExistsBySiretAsync(siret, id, cancellationToken))
        {
            return Error.Conflict("Companies.DuplicateSiret", $"A company with SIRET '{siret}' already exists.");
        }

        var slug = string.Equals(company.Name, name, StringComparison.OrdinalIgnoreCase)
            ? company.Slug
            : await ConcurrencyHelper.GenerateUniqueSlugAsync(
                name,
                candidate => companyRepository.ExistsBySlugAsync(candidate, id, cancellationToken),
                "company");

        company.Update(
            name,
            slug,
            request.LegalForm,
            request.Address,
            request.PostalCode,
            request.City,
            request.Country,
            siret,
            request.Register,
            request.Capital,
            request.CollectiveAgreement,
            request.Owner,
            request.LogoUrl,
            request.IsActive);

        await companyRepository.SaveChangesAsync(cancellationToken);
        return ToDto(company);
    }

    public async Task<Result<CompanyDto>> UpdateStatusAsync(
        Guid id,
        UpdateCompanyStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        var company = await companyRepository.GetByIdAsync(id, cancellationToken);
        if (company is null)
        {
            return Error.NotFound("Companies.NotFound", $"Company '{id}' was not found.");
        }

        var concurrency = ConcurrencyHelper.ValidateRowVersion(request.RowVersion, company.RowVersion);
        if (concurrency.IsFailure)
        {
            return concurrency.Error;
        }

        company.SetActive(request.IsActive);
        await companyRepository.SaveChangesAsync(cancellationToken);

        return ToDto(company);
    }

    private static CompanyDto ToDto(Company c) =>
        new(
            c.Id,
            c.Name,
            c.Slug,
            c.LegalForm,
            c.Address,
            c.PostalCode,
            c.City,
            c.Country,
            c.Siret,
            c.Register,
            c.Capital,
            c.CollectiveAgreement,
            c.Owner,
            c.LogoUrl,
            c.IsActive,
            c.CreatedAtUtc,
            c.UpdatedAtUtc,
            Convert.ToBase64String(c.RowVersion));
}

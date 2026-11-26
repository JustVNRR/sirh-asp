using System.Globalization;
using Sirh.BuildingBlocks.Application.Contracts;
using Sirh.BuildingBlocks.Domain.Results;
using Sirh.Modules.Contracts.Application.Abstractions;

namespace Sirh.Modules.Contracts.Application.Services;

internal sealed record BuiltContractRenderContext(
    ContractTemplateRenderModel RenderModel,
    PositionContractContextDto PositionContext,
    CompanyContractDetailsDto Company);

internal static class ContractRenderContextBuilder
{
    public static async Task<Result<BuiltContractRenderContext>> BuildAsync(
        Guid positionId,
        IReadOnlyDictionary<string, string>? customVariables,
        IHumanResourcesLookup humanResourcesLookup,
        IOrganizationLookup organizationLookup,
        CancellationToken cancellationToken)
    {
        var position = await humanResourcesLookup.GetPositionContractContextAsync(positionId, cancellationToken);
        if (position is null)
        {
            return Error.NotFound("Positions.NotFound", $"Position '{positionId}' was not found.");
        }

        var company = await organizationLookup.GetCompanyDetailsByIdAsync(position.CompanyId, cancellationToken);
        if (company is null)
        {
            return Error.NotFound("Companies.NotFound", $"Company '{position.CompanyId}' was not found.");
        }

        string? locationName = null;
        if (position.LocationId.HasValue)
        {
            locationName = await organizationLookup.GetLocationNameByIdAsync(
                position.LocationId.Value,
                cancellationToken);
        }

        var employeeDict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["first_name"] = position.EmployeeFirstName,
            ["last_name"] = position.EmployeeLastName,
            ["full_name"] = $"{position.EmployeeFirstName} {position.EmployeeLastName}".Trim(),
            ["honorific"] = position.EmployeeHonorific,
            ["email"] = position.EmployeeEmail,
            ["born_at"] = position.EmployeeBornAt?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty,
            ["birth_place"] = position.EmployeeBirthPlace ?? string.Empty,
            ["address"] = position.EmployeeAddressStreet ?? string.Empty,
            ["postal_code"] = position.EmployeeAddressPostalCode ?? string.Empty,
            ["city"] = position.EmployeeAddressCity ?? string.Empty,
            ["national_id"] = position.EmployeeNationalId ?? string.Empty
        };

        var fullCompanyAddress = string.Join(
            ", ",
            new[] { company.Address, company.PostalCode, company.City, company.Country }
                .Where(s => !string.IsNullOrWhiteSpace(s)));

        var companyDict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["name"] = company.Name,
            ["legal_form"] = company.LegalForm ?? string.Empty,
            ["address"] = fullCompanyAddress,
            ["siret"] = company.Siret,
            ["register"] = company.Register ?? string.Empty,
            ["capital"] = company.Capital ?? string.Empty,
            ["owner"] = company.Owner ?? string.Empty,
            ["collective_agreement"] = company.CollectiveAgreement
        };

        var positionDict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["job_title"] = position.JobTitle,
            ["start_at"] = position.StartAt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["end_at"] = position.EndAt?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty,
            ["hierarchical_position"] = position.HierarchicalPosition ?? string.Empty,
            ["coefficient"] = position.Coefficient ?? string.Empty,
            ["rate"] = position.Rate ?? 0m,
            ["time_contract_type"] = position.TimeContractType ?? string.Empty,
            ["time_contract_amount"] = position.TimeContractAmount ?? 0m,
            ["location_name"] = locationName ?? string.Empty,
            ["supervisor"] = position.Supervisor ?? string.Empty
        };

        var customDict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        if (customVariables is not null)
        {
            foreach (var (key, value) in customVariables)
            {
                if (!string.IsNullOrWhiteSpace(key))
                {
                    customDict[key.Trim()] = value;
                }
            }
        }

        return new BuiltContractRenderContext(
            new ContractTemplateRenderModel(employeeDict, companyDict, positionDict, customDict),
            position,
            company);
    }

    public static Result ValidateRowVersion(string? rowVersionBase64, byte[] currentRowVersion)
    {
        if (string.IsNullOrWhiteSpace(rowVersionBase64))
        {
            return Result.Success();
        }

        var trimmed = rowVersionBase64.Trim();
        Span<byte> buffer = stackalloc byte[trimmed.Length];
        if (!Convert.TryFromBase64String(trimmed, buffer, out var bytesWritten))
        {
            return Result.Failure(Error.Validation(
                "Concurrency.InvalidRowVersion",
                "RowVersion must be a valid Base64 string."));
        }

        if (currentRowVersion.Length > 0 && !buffer[..bytesWritten].SequenceEqual(currentRowVersion))
        {
            return Result.Failure(Error.Conflict(
                "Concurrency.Conflict",
                "The resource was modified concurrently. Please refresh and retry."));
        }

        return Result.Success();
    }
}

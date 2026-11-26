using Sirh.BuildingBlocks.Domain.Text;

namespace Sirh.Modules.HumanResources.Application.Services;

public static class EmployeeEmailGenerator
{
    public const int MaxLocalPartBaseLength = 60;

    public static string BuildBaseLocalPart(string firstName, string lastName)
    {
        var normalizedFirst = SlugGenerator.Generate(firstName, maxLength: 60)
            .Replace("-", string.Empty, StringComparison.Ordinal);
        var normalizedLast = SlugGenerator.Generate(lastName, maxLength: 60)
            .Replace("-", string.Empty, StringComparison.Ordinal);

        var initial = normalizedFirst.Length > 0 ? normalizedFirst[..1] : string.Empty;
        var localPart = $"{initial}{normalizedLast}";

        if (string.IsNullOrEmpty(localPart))
        {
            return "collaborateur";
        }

        return localPart.Length > MaxLocalPartBaseLength
            ? localPart[..MaxLocalPartBaseLength]
            : localPart;
    }

    public static string FormatEmail(string baseLocalPart, string companySlug, int suffix)
    {
        var domain = $"{SlugGenerator.Generate(companySlug, maxLength: 63)}.fr";
        return suffix <= 1
            ? $"{baseLocalPart}@{domain}"
            : $"{baseLocalPart}{suffix}@{domain}";
    }
}

using System.Globalization;
using System.Text;

namespace Sirh.BuildingBlocks.Domain.Text;

public static class SlugGenerator
{
    public static string Generate(string input, int maxLength = 100)
    {
        if (string.IsNullOrWhiteSpace(input) || maxLength <= 0)
        {
            return string.Empty;
        }

        var normalized = input
            .Trim()
            .Replace("œ", "oe", StringComparison.OrdinalIgnoreCase)
            .Replace("æ", "ae", StringComparison.OrdinalIgnoreCase)
            .Replace("ß", "ss", StringComparison.Ordinal)
            .Normalize(NormalizationForm.FormD);

        var builder = new StringBuilder(normalized.Length);
        var lastWasHyphen = false;

        foreach (var ch in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            var lower = char.ToLowerInvariant(ch);
            if (char.IsAsciiLetterOrDigit(lower))
            {
                builder.Append(lower);
                lastWasHyphen = false;
            }
            else if (!lastWasHyphen && builder.Length > 0)
            {
                builder.Append('-');
                lastWasHyphen = true;
            }
        }

        var slug = builder.ToString().Trim('-');
        if (slug.Length > maxLength)
        {
            slug = slug[..maxLength].TrimEnd('-');
        }

        return slug;
    }
}

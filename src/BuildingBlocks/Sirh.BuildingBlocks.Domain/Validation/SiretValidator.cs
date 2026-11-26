namespace Sirh.BuildingBlocks.Domain.Validation;

public static class SiretValidator
{
    public const int SiretLength = 14;
    private const string LaPosteSirenPrefix = "356000000";

    public static bool IsValid(string? siret)
    {
        if (string.IsNullOrWhiteSpace(siret))
        {
            return false;
        }

        var trimmed = siret.Trim();
        if (trimmed.Length != SiretLength)
        {
            return false;
        }

        var luhnSum = 0;
        var rawSum = 0;

        for (var i = 0; i < SiretLength; i++)
        {
            var ch = trimmed[i];
            if (!char.IsAsciiDigit(ch))
            {
                return false;
            }

            var digit = ch - '0';
            rawSum += digit;

            if (i % 2 == 0)
            {
                var doubled = digit * 2;
                luhnSum += doubled > 9 ? doubled - 9 : doubled;
            }
            else
            {
                luhnSum += digit;
            }
        }

        if (luhnSum % 10 == 0)
        {
            return true;
        }

        return trimmed.StartsWith(LaPosteSirenPrefix, StringComparison.Ordinal) && rawSum % 5 == 0;
    }
}

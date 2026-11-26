using Shouldly;
using Sirh.BuildingBlocks.Domain.Validation;
using Xunit;

namespace Sirh.UnitTests.Organization;

public sealed class SiretValidatorTests
{
    [Theory]
    [InlineData("73282932000074", true)]
    [InlineData("44306184100047", true)]
    [InlineData("55203253400646", true)]
    [InlineData("35600000010005", true)] // La Poste modulo-5 rule
    [InlineData("73282932000075", false)]
    [InlineData("7328293200007", false)]
    [InlineData("732829320000741", false)]
    [InlineData("7328293200007A", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsValid_Should_Check_French_Siret_Rules(string? siret, bool expected)
    {
        SiretValidator.IsValid(siret).ShouldBe(expected);
    }
}

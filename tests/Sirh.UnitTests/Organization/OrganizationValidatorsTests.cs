using Shouldly;
using Sirh.Modules.Organization.Application.DTOs;
using Sirh.Modules.Organization.Application.Validators;
using Xunit;

namespace Sirh.UnitTests.Organization;

public sealed class OrganizationValidatorsTests
{
    [Fact]
    public void CreateCompanyValidator_Should_Validate_Siret_And_LogoUrl()
    {
        var validator = new CreateCompanyRequestValidator();

        validator.Validate(new CreateCompanyRequest(
            "Acme France",
            "SAS",
            "10 Rue de la Paix",
            "75002",
            "Paris",
            "France",
            "44306184100047",
            null,
            null,
            null,
            null,
            "https://example.com/logo.png")).IsValid.ShouldBeTrue();

        validator.Validate(new CreateCompanyRequest(
            "Acme France",
            null,
            "10 Rue de la Paix",
            null,
            null,
            null,
            "12345678901234",
            null,
            null,
            null,
            null,
            null)).IsValid.ShouldBeFalse();

        validator.Validate(new CreateCompanyRequest(
            "Acme France",
            null,
            "10 Rue de la Paix",
            null,
            null,
            null,
            "44306184100047",
            null,
            null,
            null,
            null,
            "ftp://invalid.local/logo.png")).IsValid.ShouldBeFalse();
    }

    [Theory]
    [InlineData("#2563EB", null, true)]
    [InlineData("#1a2b3c", "55203253400646", true)]
    [InlineData("2563EB", null, false)]
    [InlineData("#GGGGGG", null, false)]
    [InlineData("#2563EB", "bad-siret", false)]
    public void CreateLocationValidator_Should_Validate_ColorCode_And_Optional_Siret(
        string colorCode,
        string? siret,
        bool expectedValid)
    {
        var validator = new CreateLocationRequestValidator();
        var result = validator.Validate(new CreateLocationRequest(
            "Agence Lyon",
            "20 Place Bellecour",
            "69002",
            "Lyon",
            "France",
            "69",
            siret,
            colorCode));

        result.IsValid.ShouldBe(expectedValid);
    }

    [Theory]
    [InlineData("STAGE_6M", true)]
    [InlineData("INTERIM-2026", true)]
    [InlineData("BAD CODE", false)]
    [InlineData("", false)]
    public void CreateContractTypeValidator_Should_Validate_Code_Format(string code, bool expectedValid)
    {
        var validator = new CreateContractTypeRequestValidator();
        var result = validator.Validate(new CreateContractTypeRequest(code, "Label test"));
        result.IsValid.ShouldBe(expectedValid);
    }
}

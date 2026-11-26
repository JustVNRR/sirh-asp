using Shouldly;
using Sirh.Modules.HumanResources.Application.DTOs;
using Sirh.Modules.HumanResources.Application.Validators;
using Xunit;

namespace Sirh.UnitTests.HumanResources;

public sealed class EmployeeValidatorsTests
{
    [Fact]
    public void CreateEmployeeValidator_Should_Accept_Valid_Request()
    {
        var validator = new CreateEmployeeRequestValidator();
        var request = new CreateEmployeeRequest(
            Guid.CreateVersion7(),
            null,
            "Mme",
            "Alice",
            "Durand",
            "StrongPass123!",
            ["EMPLOYEE"],
            PersonalEmail: "alice.perso@example.com");

        validator.Validate(request).IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData("Dr", "StrongPass123!", "alice@example.com", false)]
    [InlineData("M", "weak", "alice@example.com", false)]
    [InlineData("Mme", "StrongPass123!", "not-an-email", false)]
    public void CreateEmployeeValidator_Should_Reject_Invalid_Fields(
        string honorific,
        string password,
        string personalEmail,
        bool expectedValid)
    {
        var validator = new CreateEmployeeRequestValidator();
        var request = new CreateEmployeeRequest(
            Guid.CreateVersion7(),
            null,
            honorific,
            "Alice",
            "Durand",
            password,
            ["EMPLOYEE"],
            PersonalEmail: personalEmail);

        validator.Validate(request).IsValid.ShouldBe(expectedValid);
    }

    [Fact]
    public void UpdateEmployeeValidator_Should_Reject_Empty_RoleCodes_When_Provided()
    {
        var validator = new UpdateEmployeeRequestValidator();
        var request = new UpdateEmployeeRequest(
            Guid.CreateVersion7(),
            null,
            "M",
            "Paul",
            "Martin",
            RoleCodes: []);

        validator.Validate(request).IsValid.ShouldBeFalse();
    }
}

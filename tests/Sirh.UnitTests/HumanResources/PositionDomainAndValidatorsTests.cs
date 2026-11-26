using Shouldly;
using Sirh.Modules.HumanResources.Application.DTOs;
using Sirh.Modules.HumanResources.Application.Validators;
using Sirh.Modules.HumanResources.Domain.Entities;
using Xunit;

namespace Sirh.UnitTests.HumanResources;

public sealed class PositionDomainAndValidatorsTests
{
    [Fact]
    public void Position_IsActiveOn_Should_Respect_Start_And_End_Boundaries()
    {
        var position = new Position(
            "poste-alice",
            Guid.CreateVersion7(),
            null,
            null,
            "Développeuse .NET",
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 6, 30),
            null,
            null,
            null,
            null,
            "Hours",
            35m,
            null,
            ["DEPT_OTHER"]);

        position.IsActiveOn(new DateOnly(2025, 12, 31)).ShouldBeFalse();
        position.IsActiveOn(new DateOnly(2026, 1, 1)).ShouldBeTrue();
        position.IsActiveOn(new DateOnly(2026, 6, 30)).ShouldBeTrue();
        position.IsActiveOn(new DateOnly(2026, 7, 1)).ShouldBeFalse();
    }

    [Fact]
    public void EmergencyContactValidator_Should_Require_Phone_And_Valid_Priority()
    {
        var validator = new CreateEmergencyContactRequestValidator();

        validator.Validate(new CreateEmergencyContactRequest(
            "Marie Durand",
            "Conjointe",
            MobilePhone: "0601020304",
            PriorityOrder: 1)).IsValid.ShouldBeTrue();

        validator.Validate(new CreateEmergencyContactRequest(
            "Marie Durand",
            "Conjointe",
            PriorityOrder: 1)).IsValid.ShouldBeFalse();

        validator.Validate(new CreateEmergencyContactRequest(
            "Marie Durand",
            "Conjointe",
            MobilePhone: "0601020304",
            PriorityOrder: 3)).IsValid.ShouldBeFalse();
    }

    [Fact]
    public void PositionValidator_Should_Reject_Invalid_Dates_SelfManager_NegativeRate_And_TimeType()
    {
        var validator = new CreatePositionRequestValidator();
        var employeeId = Guid.CreateVersion7();

        validator.Validate(new CreatePositionRequest(
            employeeId,
            Guid.CreateVersion7(),
            null,
            "Ingénieur",
            new DateOnly(2026, 5, 1),
            new DateOnly(2026, 4, 30))).IsValid.ShouldBeFalse();

        validator.Validate(new CreatePositionRequest(
            employeeId,
            employeeId,
            null,
            "Ingénieur",
            new DateOnly(2026, 5, 1))).IsValid.ShouldBeFalse();

        validator.Validate(new CreatePositionRequest(
            employeeId,
            null,
            null,
            "Ingénieur",
            new DateOnly(2026, 5, 1),
            Rate: -10m)).IsValid.ShouldBeFalse();

        validator.Validate(new CreatePositionRequest(
            employeeId,
            null,
            null,
            "Ingénieur",
            new DateOnly(2026, 5, 1),
            TimeContractType: "Weeks")).IsValid.ShouldBeFalse();
    }
}

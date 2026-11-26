using NetArchTest.Rules;
using Shouldly;
using Sirh.BuildingBlocks.Application.Abstractions;
using Sirh.BuildingBlocks.Domain.Primitives;
using Sirh.Modules.Contracts.Domain.Entities;
using Sirh.Modules.Health.Domain.Entities;
using Sirh.Modules.HumanResources.Domain.Entities;
using Sirh.Modules.Identity.Domain.Entities;
using Sirh.Modules.Organization.Domain.Entities;
using Xunit;

namespace Sirh.ArchitectureTests;

public sealed class ArchitectureTests
{
    private const string ApplicationNamespace = "Sirh.BuildingBlocks.Application";
    private const string InfrastructureNamespace = "Sirh.BuildingBlocks.Infrastructure";
    private const string ApiNamespace = "Sirh.Api";

    [Fact]
    public void Domain_Should_Not_Depend_On_Application_Infrastructure_Or_Api()
    {
        var result = Types.InAssembly(typeof(Entity).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                ApplicationNamespace,
                InfrastructureNamespace,
                ApiNamespace,
                "Microsoft.EntityFrameworkCore",
                "Microsoft.AspNetCore")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(
            $"Domain layer violations: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Fact]
    public void Application_Should_Not_Depend_On_Infrastructure_Or_Api()
    {
        var result = Types.InAssembly(typeof(ICurrentUser).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                InfrastructureNamespace,
                ApiNamespace,
                "Microsoft.EntityFrameworkCore")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(
            $"Application layer violations: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Fact]
    public void Identity_Domain_Should_Not_Depend_On_Infrastructure_Presentation_Or_EfCore()
    {
        var result = Types.InAssembly(typeof(RefreshToken).Assembly)
            .That()
            .ResideInNamespace("Sirh.Modules.Identity.Domain")
            .ShouldNot()
            .HaveDependencyOnAny(
                "Sirh.Modules.Identity.Infrastructure",
                "Sirh.Modules.Identity.Presentation",
                "Microsoft.EntityFrameworkCore")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(
            $"Identity.Domain violations: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Fact]
    public void Identity_Application_Should_Not_Depend_On_Infrastructure_Presentation_Or_EfCore()
    {
        var result = Types.InAssembly(typeof(RefreshToken).Assembly)
            .That()
            .ResideInNamespace("Sirh.Modules.Identity.Application")
            .ShouldNot()
            .HaveDependencyOnAny(
                "Sirh.Modules.Identity.Infrastructure",
                "Sirh.Modules.Identity.Presentation",
                "Microsoft.EntityFrameworkCore")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(
            $"Identity.Application violations: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Fact]
    public void Organization_Domain_Should_Not_Depend_On_Infrastructure_Presentation_EfCore_Or_Identity()
    {
        var result = Types.InAssembly(typeof(Company).Assembly)
            .That()
            .ResideInNamespace("Sirh.Modules.Organization.Domain")
            .ShouldNot()
            .HaveDependencyOnAny(
                "Sirh.Modules.Organization.Infrastructure",
                "Sirh.Modules.Organization.Presentation",
                "Sirh.Modules.Identity",
                "Microsoft.EntityFrameworkCore")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(
            $"Organization.Domain violations: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Fact]
    public void Organization_Application_Should_Not_Depend_On_Infrastructure_Presentation_EfCore_Or_Identity()
    {
        var result = Types.InAssembly(typeof(Company).Assembly)
            .That()
            .ResideInNamespace("Sirh.Modules.Organization.Application")
            .ShouldNot()
            .HaveDependencyOnAny(
                "Sirh.Modules.Organization.Infrastructure",
                "Sirh.Modules.Organization.Presentation",
                "Sirh.Modules.Identity",
                "Microsoft.EntityFrameworkCore")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(
            $"Organization.Application violations: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Fact]
    public void Organization_Module_Should_Not_Depend_On_Identity_Module()
    {
        var result = Types.InAssembly(typeof(Company).Assembly)
            .ShouldNot()
            .HaveDependencyOn("Sirh.Modules.Identity")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(
            $"Organization -> Identity coupling violations: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Fact]
    public void HumanResources_Domain_Should_Not_Depend_On_Infrastructure_Presentation_EfCore_Or_Other_Modules()
    {
        var result = Types.InAssembly(typeof(Employee).Assembly)
            .That()
            .ResideInNamespace("Sirh.Modules.HumanResources.Domain")
            .ShouldNot()
            .HaveDependencyOnAny(
                "Sirh.Modules.HumanResources.Infrastructure",
                "Sirh.Modules.HumanResources.Presentation",
                "Sirh.Modules.Identity",
                "Sirh.Modules.Organization",
                "Microsoft.EntityFrameworkCore")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(
            $"HumanResources.Domain violations: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Fact]
    public void HumanResources_Application_Should_Not_Depend_On_Infrastructure_Presentation_EfCore_Or_Other_Modules()
    {
        var result = Types.InAssembly(typeof(Employee).Assembly)
            .That()
            .ResideInNamespace("Sirh.Modules.HumanResources.Application")
            .ShouldNot()
            .HaveDependencyOnAny(
                "Sirh.Modules.HumanResources.Infrastructure",
                "Sirh.Modules.HumanResources.Presentation",
                "Sirh.Modules.Identity",
                "Sirh.Modules.Organization",
                "Microsoft.EntityFrameworkCore")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(
            $"HumanResources.Application violations: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Fact]
    public void HumanResources_Module_Should_Not_Depend_On_Identity_Or_Organization_Modules()
    {
        var result = Types.InAssembly(typeof(Employee).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny("Sirh.Modules.Identity", "Sirh.Modules.Organization")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(
            $"HumanResources cross-module coupling violations: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Fact]
    public void Contracts_Domain_Should_Not_Depend_On_Infrastructure_Presentation_EfCore_Or_Other_Modules()
    {
        var result = Types.InAssembly(typeof(Contract).Assembly)
            .That()
            .ResideInNamespace("Sirh.Modules.Contracts.Domain")
            .ShouldNot()
            .HaveDependencyOnAny(
                "Sirh.Modules.Contracts.Infrastructure",
                "Sirh.Modules.Contracts.Presentation",
                "Sirh.Modules.Identity",
                "Sirh.Modules.Organization",
                "Sirh.Modules.HumanResources",
                "Microsoft.EntityFrameworkCore")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(
            $"Contracts.Domain violations: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Fact]
    public void Contracts_Application_Should_Not_Depend_On_Infrastructure_Presentation_EfCore_Or_Other_Modules()
    {
        var result = Types.InAssembly(typeof(Contract).Assembly)
            .That()
            .ResideInNamespace("Sirh.Modules.Contracts.Application")
            .ShouldNot()
            .HaveDependencyOnAny(
                "Sirh.Modules.Contracts.Infrastructure",
                "Sirh.Modules.Contracts.Presentation",
                "Sirh.Modules.Identity",
                "Sirh.Modules.Organization",
                "Sirh.Modules.HumanResources",
                "Microsoft.EntityFrameworkCore")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(
            $"Contracts.Application violations: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Fact]
    public void Contracts_Module_Should_Not_Depend_On_Identity_Organization_Or_HumanResources_Modules()
    {
        var result = Types.InAssembly(typeof(Contract).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "Sirh.Modules.Identity",
                "Sirh.Modules.Organization",
                "Sirh.Modules.HumanResources")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(
            $"Contracts cross-module coupling violations: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Fact]
    public void Health_Domain_Should_Not_Depend_On_Infrastructure_Presentation_EfCore_Or_Other_Modules()
    {
        var result = Types.InAssembly(typeof(MedicalVisit).Assembly)
            .That()
            .ResideInNamespace("Sirh.Modules.Health.Domain")
            .ShouldNot()
            .HaveDependencyOnAny(
                "Sirh.Modules.Health.Infrastructure",
                "Sirh.Modules.Health.Presentation",
                "Sirh.Modules.Identity",
                "Sirh.Modules.Organization",
                "Sirh.Modules.HumanResources",
                "Sirh.Modules.Contracts",
                "Microsoft.EntityFrameworkCore")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(
            $"Health.Domain violations: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Fact]
    public void Health_Application_Should_Not_Depend_On_Infrastructure_Presentation_EfCore_Or_Other_Modules()
    {
        var result = Types.InAssembly(typeof(MedicalVisit).Assembly)
            .That()
            .ResideInNamespace("Sirh.Modules.Health.Application")
            .ShouldNot()
            .HaveDependencyOnAny(
                "Sirh.Modules.Health.Infrastructure",
                "Sirh.Modules.Health.Presentation",
                "Sirh.Modules.Identity",
                "Sirh.Modules.Organization",
                "Sirh.Modules.HumanResources",
                "Sirh.Modules.Contracts",
                "Microsoft.EntityFrameworkCore")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(
            $"Health.Application violations: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Fact]
    public void Health_Module_Should_Not_Depend_On_Identity_Organization_HumanResources_Or_Contracts_Modules()
    {
        var result = Types.InAssembly(typeof(MedicalVisit).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "Sirh.Modules.Identity",
                "Sirh.Modules.Organization",
                "Sirh.Modules.HumanResources",
                "Sirh.Modules.Contracts")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(
            $"Health cross-module coupling violations: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }
}

using Shouldly;
using Sirh.Modules.HumanResources.Application.Services;
using Xunit;

namespace Sirh.UnitTests.HumanResources;

public sealed class EmployeeEmailGeneratorTests
{
    [Theory]
    [InlineData("Alice", "Durand", "nova-conseil", 1, "adurand@nova-conseil.fr")]
    [InlineData("Alice", "Durand", "nova-conseil", 2, "adurand2@nova-conseil.fr")]
    [InlineData("Élodie", "Cœur-D'Acier", "nova-conseil", 1, "ecoeurdacier@nova-conseil.fr")]
    [InlineData("Jean-Luc", "De La Fontaine", "groupe-alpha", 3, "jdelafontaine3@groupe-alpha.fr")]
    public void BuildAndFormat_Should_Generate_Clean_Company_Email(
        string firstName,
        string lastName,
        string companySlug,
        int suffix,
        string expected)
    {
        var localBase = EmployeeEmailGenerator.BuildBaseLocalPart(firstName, lastName);
        localBase.Length.ShouldBeLessThanOrEqualTo(EmployeeEmailGenerator.MaxLocalPartBaseLength);

        EmployeeEmailGenerator.FormatEmail(localBase, companySlug, suffix).ShouldBe(expected);
    }

    [Fact]
    public void BuildBaseLocalPart_Should_Truncate_Very_Long_LastNames()
    {
        var longLastName = new string('a', 90);
        var localBase = EmployeeEmailGenerator.BuildBaseLocalPart("Jean", longLastName);
        localBase.Length.ShouldBe(EmployeeEmailGenerator.MaxLocalPartBaseLength);
    }
}

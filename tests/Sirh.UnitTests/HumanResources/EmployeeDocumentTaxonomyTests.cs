using Shouldly;
using Sirh.Modules.HumanResources.Domain.Constants;
using Sirh.Modules.HumanResources.Domain.Enums;
using Xunit;

namespace Sirh.UnitTests.HumanResources;

public sealed class EmployeeDocumentTaxonomyTests
{
    [Fact]
    public void Every_DocumentType_Should_Map_To_A_Valid_Category()
    {
        var allTypes = Enum.GetValues<EmployeeDocumentType>();

        foreach (var documentType in allTypes)
        {
            var category = EmployeeDocumentTaxonomy.GetCategory(documentType);
            Enum.IsDefined(category).ShouldBeTrue();
        }
    }

    [Theory]
    [InlineData(EmployeeDocumentType.Cv, EmployeeDocumentCategory.PersonalFile, false, true)]
    [InlineData(EmployeeDocumentType.RqthCertificate, EmployeeDocumentCategory.PersonalFile, false, true)]
    [InlineData(EmployeeDocumentType.ResidentPermit, EmployeeDocumentCategory.PersonalFile, false, true)]
    [InlineData(EmployeeDocumentType.MaternityLeave, EmployeeDocumentCategory.PersonalEvents, false, true)]
    [InlineData(EmployeeDocumentType.Amendment, EmployeeDocumentCategory.ContractualElements, false, false)]
    [InlineData(EmployeeDocumentType.Warning, EmployeeDocumentCategory.Disciplinary, true, false)]
    [InlineData(EmployeeDocumentType.MutualBreachAgreement, EmployeeDocumentCategory.Departure, true, false)]
    [InlineData(EmployeeDocumentType.IntegrationFile, EmployeeDocumentCategory.Integration, false, false)]
    public void Taxonomy_Should_Classify_Categories_And_Sensitivity_Accurately(
        EmployeeDocumentType documentType,
        EmployeeDocumentCategory expectedCategory,
        bool expectedSensitive,
        bool expectedSelfServiceWritable)
    {
        var category = EmployeeDocumentTaxonomy.GetCategory(documentType);

        category.ShouldBe(expectedCategory);
        EmployeeDocumentTaxonomy.IsSensitiveCategory(category).ShouldBe(expectedSensitive);
        EmployeeDocumentTaxonomy.IsSensitiveType(documentType).ShouldBe(expectedSensitive);
        EmployeeDocumentTaxonomy.IsSelfServiceWritableCategory(category).ShouldBe(expectedSelfServiceWritable);
    }
}

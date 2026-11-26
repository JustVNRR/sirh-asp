using Shouldly;
using Sirh.BuildingBlocks.Domain.Text;
using Xunit;

namespace Sirh.UnitTests.Organization;

public sealed class SlugGeneratorTests
{
    [Fact]
    public void Generate_Should_Strip_Accents_And_Punctuation()
    {
        SlugGenerator.Generate("Établissement Île-de-France — Siège & Pôle")
            .ShouldBe("etablissement-ile-de-france-siege-pole");

        SlugGenerator.Generate("Cœur d'œuvre")
            .ShouldBe("coeur-d-oeuvre");
    }

    [Fact]
    public void Generate_Should_Respect_MaxLength_Without_Trailing_Hyphen()
    {
        var slug = SlugGenerator.Generate("Nova Conseil France Paris", maxLength: 13);
        slug.ShouldBe("nova-conseil");
    }

    [Fact]
    public void Generate_Should_Return_Empty_For_Blank_Input()
    {
        SlugGenerator.Generate("   ").ShouldBe(string.Empty);
    }
}

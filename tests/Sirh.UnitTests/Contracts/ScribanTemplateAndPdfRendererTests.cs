using System.Text;
using Shouldly;
using Sirh.Modules.Contracts.Application.Abstractions;
using Sirh.Modules.Contracts.Infrastructure.Documents;
using Xunit;

namespace Sirh.UnitTests.Contracts;

public sealed class ScribanTemplateAndPdfRendererTests
{
    [Fact]
    public void ValidateSyntax_Should_Accept_Valid_Template_And_Reject_Broken_Scriban()
    {
        var engine = new ScribanContractTemplateEngine();

        engine.ValidateSyntax("Bonjour {{ employee.full_name }}, poste {{ position.job_title }}.")
            .IsValid.ShouldBeTrue();

        var invalid = engine.ValidateSyntax("{{ if employee.first_name }}Pas de end");
        invalid.IsValid.ShouldBeFalse();
        invalid.Errors.ShouldNotBeEmpty();
    }

    [Fact]
    public void Render_Should_Interpolate_Standard_And_Custom_Variables_And_QuestPdf_Should_Output_Pdf()
    {
        var engine = new ScribanContractTemplateEngine();
        var renderer = new QuestPdfContractRenderer();

        var model = new ContractTemplateRenderModel(
            new Dictionary<string, object?> { ["full_name"] = "Alice Durand" },
            new Dictionary<string, object?> { ["name"] = "Nova Conseil" },
            new Dictionary<string, object?> { ["job_title"] = "Développeuse .NET", ["rate"] = 4200m },
            new Dictionary<string, object?> { ["clause_teletravail"] = "2 jours par semaine" });

        const string template = """
            # CONTRAT DE TRAVAIL CDI

            Entre **{{ company.name }}** et **{{ employee.full_name }}**.

            ## Article 1 — Poste
            - Intitulé : {{ position.job_title }}
            {{ if position.rate > 0 }}- Rémunération : {{ position.rate }} EUR brut/mois{{ end }}
            - Télétravail : {{ custom.clause_teletravail }}
            """;

        var rendered = engine.Render(template, model);
        rendered.IsSuccess.ShouldBeTrue();
        rendered.Value.ShouldContain("Alice Durand");
        rendered.Value.ShouldContain("4200 EUR");
        rendered.Value.ShouldContain("2 jours par semaine");

        var pdfBytes = renderer.RenderPdf("Contrat CDI", "Nova Conseil", rendered.Value);
        pdfBytes.Length.ShouldBeGreaterThan(500);
        Encoding.ASCII.GetString(pdfBytes.AsSpan(0, 5)).ShouldBe("%PDF-");
    }
}

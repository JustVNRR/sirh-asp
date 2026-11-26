using Sirh.BuildingBlocks.Domain.Results;

namespace Sirh.Modules.Contracts.Application.Abstractions;

public sealed record TemplateSyntaxResult(bool IsValid, IReadOnlyList<string> Errors);

public sealed record ContractTemplateRenderModel(
    IReadOnlyDictionary<string, object?> Employee,
    IReadOnlyDictionary<string, object?> Company,
    IReadOnlyDictionary<string, object?> Position,
    IReadOnlyDictionary<string, object?> Custom);

public interface IContractTemplateEngine
{
    TemplateSyntaxResult ValidateSyntax(string templateContent);

    Result<string> Render(string templateContent, ContractTemplateRenderModel model);
}

public interface IPdfContractRenderer
{
    byte[] RenderPdf(string documentTitle, string companyName, string renderedMarkdown);
}

using Scriban;
using Scriban.Runtime;
using Scriban.Syntax;
using Sirh.BuildingBlocks.Domain.Results;
using Sirh.Modules.Contracts.Application.Abstractions;

namespace Sirh.Modules.Contracts.Infrastructure.Documents;

public sealed class ScribanContractTemplateEngine : IContractTemplateEngine
{
    public TemplateSyntaxResult ValidateSyntax(string templateContent)
    {
        if (string.IsNullOrWhiteSpace(templateContent))
        {
            return new TemplateSyntaxResult(false, ["Template content cannot be empty."]);
        }

        var template = Template.Parse(templateContent);
        if (!template.HasErrors)
        {
            return new TemplateSyntaxResult(true, []);
        }

        var errors = template.Messages.Select(m => m.ToString()).ToArray();
        return new TemplateSyntaxResult(false, errors);
    }

    public Result<string> Render(string templateContent, ContractTemplateRenderModel model)
    {
        var template = Template.Parse(templateContent);
        if (template.HasErrors)
        {
            var errors = string.Join(" | ", template.Messages.Select(m => m.ToString()));
            return Error.Validation("ContractModels.InvalidTemplateSyntax", errors);
        }

        try
        {
            var root = new ScriptObject
            {
                ["employee"] = ToScriptObject(model.Employee),
                ["company"] = ToScriptObject(model.Company),
                ["position"] = ToScriptObject(model.Position),
                ["custom"] = ToScriptObject(model.Custom)
            };

            foreach (var (key, value) in model.Custom)
            {
                if (!root.ContainsKey(key))
                {
                    root[key] = value;
                }
            }

            var context = new TemplateContext
            {
                LoopLimit = 1000,
                RecursiveLimit = 50,
                MemberRenamer = member => member.Name
            };
            context.PushGlobal(root);

            var rendered = template.Render(context);
            return Result.Success(rendered);
        }
        catch (ScriptRuntimeException ex)
        {
            return Error.Validation("ContractModels.TemplateRenderError", ex.Message);
        }
    }

    private static ScriptObject ToScriptObject(IReadOnlyDictionary<string, object?> source)
    {
        var scriptObject = new ScriptObject();
        foreach (var (key, value) in source)
        {
            scriptObject[key] = value;
        }

        return scriptObject;
    }
}

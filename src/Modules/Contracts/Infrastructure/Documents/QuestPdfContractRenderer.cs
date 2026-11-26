using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Sirh.Modules.Contracts.Application.Abstractions;

namespace Sirh.Modules.Contracts.Infrastructure.Documents;

public sealed class QuestPdfContractRenderer : IPdfContractRenderer
{
    static QuestPdfContractRenderer()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public byte[] RenderPdf(string documentTitle, string companyName, string renderedMarkdown)
    {
        var lines = renderedMarkdown
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n');

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(style => style.FontSize(10.5f).LineHeight(1.35f).FontColor(Colors.Grey.Darken4));

                page.Header().PaddingBottom(8).BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Column(header =>
                {
                    header.Item().Text(companyName).FontSize(10).SemiBold().FontColor(Colors.Grey.Darken1);
                    header.Item().Text(documentTitle).FontSize(14).Bold().FontColor(Colors.Blue.Darken3);
                });

                page.Content().PaddingVertical(12).Column(column =>
                {
                    column.Spacing(4);

                    foreach (var rawLine in lines)
                    {
                        var line = rawLine.Trim();
                        if (string.IsNullOrEmpty(line))
                        {
                            column.Item().Height(4);
                            continue;
                        }

                        if (line is "---" or "***")
                        {
                            column.Item().PaddingVertical(4).LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten1);
                            continue;
                        }

                        if (line.StartsWith("### ", StringComparison.Ordinal))
                        {
                            column.Item().PaddingTop(6).Text(line[4..].Trim()).FontSize(11.5f).SemiBold();
                            continue;
                        }

                        if (line.StartsWith("## ", StringComparison.Ordinal))
                        {
                            column.Item().PaddingTop(8).Text(line[3..].Trim()).FontSize(13).SemiBold();
                            continue;
                        }

                        if (line.StartsWith("# ", StringComparison.Ordinal))
                        {
                            column.Item().PaddingTop(10).Text(line[2..].Trim()).FontSize(15).Bold().FontColor(Colors.Blue.Darken3);
                            continue;
                        }

                        if (line.StartsWith("- ", StringComparison.Ordinal) || line.StartsWith("* ", StringComparison.Ordinal))
                        {
                            var bulletContent = line[2..].Trim();
                            column.Item().PaddingLeft(12).Row(row =>
                            {
                                row.AutoItem().Text("•  ");
                                row.RelativeItem().Text(text => RenderFormattedSpans(text, bulletContent));
                            });
                            continue;
                        }

                        column.Item().Text(text => RenderFormattedSpans(text, line));
                    }
                });

                page.Footer().AlignRight().Text(text =>
                {
                    text.Span("Page ").FontSize(9).FontColor(Colors.Grey.Darken1);
                    text.CurrentPageNumber().FontSize(9).FontColor(Colors.Grey.Darken1);
                    text.Span(" / ").FontSize(9).FontColor(Colors.Grey.Darken1);
                    text.TotalPages().FontSize(9).FontColor(Colors.Grey.Darken1);
                });
            });
        });

        return document.GeneratePdf();
    }

    private static void RenderFormattedSpans(TextDescriptor text, string line)
    {
        var segments = line.Split("**", StringSplitOptions.None);
        for (var i = 0; i < segments.Length; i++)
        {
            if (segments[i].Length == 0)
            {
                continue;
            }

            if (i % 2 == 1)
            {
                text.Span(segments[i]).SemiBold();
            }
            else
            {
                text.Span(segments[i]);
            }
        }
    }
}

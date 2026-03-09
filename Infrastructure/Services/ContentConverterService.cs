using PromptMasterv6.Core.Interfaces;
using ReverseMarkdown;
using System;
using System.Text.RegularExpressions;

namespace PromptMasterv6.Infrastructure.Services;

public class ContentConverterService : IContentConverterService
{
    private static readonly Regex HtmlTagRegex = new(@"<[^>]+>", RegexOptions.Compiled);

    public string? ConvertHtmlToMarkdown(string? content)
    {
        if (string.IsNullOrWhiteSpace(content)) return content;

        try
        {
            var converter = new Converter();
            return converter.Convert(content);
        }
        catch (Exception ex)
        {
            LoggerService.Instance.LogException(ex, "HTML转Markdown失败", "ContentConverterService.ConvertHtmlToMarkdown");
            return content;
        }
    }

    public bool ContainsHtml(string content)
    {
        return HtmlTagRegex.IsMatch(content);
    }
}

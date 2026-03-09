using ReverseMarkdown;
using System;
using System.Text.RegularExpressions;

namespace PromptMasterv6.Infrastructure.Services;

public class ContentConverterService
{
    private static readonly Regex HtmlTagRegex = new(@"<[^>]+>", RegexOptions.Compiled);
    private readonly LoggerService _logger;

    public ContentConverterService(LoggerService logger)
    {
        _logger = logger;
    }

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
            _logger.LogException(ex, "HTML转Markdown失败", "ContentConverterService.ConvertHtmlToMarkdown");
            return content;
        }
    }

    public bool ContainsHtml(string content)
    {
        return HtmlTagRegex.IsMatch(content);
    }
}

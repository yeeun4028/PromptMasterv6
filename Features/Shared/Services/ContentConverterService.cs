using PromptMasterv6.Infrastructure.Services;
using ReverseMarkdown;
using System;
using System.Text.RegularExpressions;

namespace PromptMasterv6.Features.Shared.Services;

public interface IContentConverterService
{
    string? ConvertHtmlToMarkdown(string? content);
    bool ContainsHtml(string content);
}

public class ContentConverterService : IContentConverterService
{
    private static readonly Regex HtmlTagRegex = new(
        @"<(p|div|br|span|a|img|ul|ol|li|table|tr|td|th|h[1-6]|strong|em|b|i|u|code|pre|blockquote|script|style|iframe|form|input|button)[\s>/]",
        RegexOptions.IgnoreCase | RegexOptions.Compiled,
        TimeSpan.FromMilliseconds(100)
    );

    public string? ConvertHtmlToMarkdown(string? content)
    {
        if (string.IsNullOrWhiteSpace(content)) return content;

        if (ContainsHtml(content))
        {
            try
            {
                var config = new Config
                {
                    UnknownTags = Config.UnknownTagsOption.PassThrough,
                    GithubFlavored = true,
                    RemoveComments = false
                };

                var converter = new Converter(config);
                return converter.Convert(content);
            }
            catch (Exception ex)
            {
                LoggerService.Instance.LogException(ex, "HTML转Markdown失败", "ContentConverterService.ConvertHtmlToMarkdown");
                return content;
            }
        }

        return content;
    }

    public bool ContainsHtml(string content)
    {
        if (string.IsNullOrEmpty(content)) return false;
        try
        {
            return HtmlTagRegex.IsMatch(content);
        }
        catch (RegexMatchTimeoutException)
        {
            return false;
        }
    }
}

namespace PromptMasterv6.Core.Interfaces
{
    public interface IContentConverterService
    {
        string? ConvertHtmlToMarkdown(string? content);
        bool ContainsHtml(string content);
    }
}

using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using PromptMasterv6.Infrastructure.Services;

namespace PromptMasterv6.Features.Shared.Queries;

public record ParseVariablesQuery(string? Content) : IRequest<List<string>>;

public class ParseVariablesHandler : IRequestHandler<ParseVariablesQuery, List<string>>
{
    private static readonly Regex VariableRegex = new(@"\{\{(.*?)\}\}", RegexOptions.Compiled);

    public Task<List<string>> Handle(ParseVariablesQuery request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(request.Content))
        {
            return Task.FromResult(new List<string>());
        }

        var matches = VariableRegex.Matches(request.Content);
        var varNames = matches.Cast<Match>()
            .Select(m => m.Groups[1].Value.Trim())
            .Where(s => !string.IsNullOrEmpty(s))
            .Distinct()
            .ToList();

        return Task.FromResult(varNames);
    }
}

public record CompileContentQuery(string? Content, Dictionary<string, string> Variables, string? AdditionalInput = null) : IRequest<string>;

public class CompileContentHandler : IRequestHandler<CompileContentQuery, string>
{
    public Task<string> Handle(CompileContentQuery request, CancellationToken cancellationToken)
    {
        string finalContent = request.Content ?? "";
        
        if (request.Variables != null)
        {
            foreach (var kvp in request.Variables)
            {
                finalContent = finalContent.Replace("{{" + kvp.Key + "}}", kvp.Value ?? "");
            }
        }

        if (!string.IsNullOrWhiteSpace(request.AdditionalInput))
        {
            if (finalContent.Contains("{{input}}"))
            {
                finalContent = finalContent.Replace("{{input}}", request.AdditionalInput);
            }
            else
            {
                finalContent = finalContent + Environment.NewLine + Environment.NewLine + request.AdditionalInput;
            }
        }
        else
        {
            finalContent = finalContent.Replace("{{input}}", "");
        }

        return Task.FromResult(finalContent.TrimEnd());
    }
}

public record ConvertHtmlToMarkdownQuery(string? Content) : IRequest<string?>;

public class ConvertHtmlToMarkdownHandler : IRequestHandler<ConvertHtmlToMarkdownQuery, string?>
{
    private readonly LoggerService _logger;

    public ConvertHtmlToMarkdownHandler(LoggerService logger)
    {
        _logger = logger;
    }

    public Task<string?> Handle(ConvertHtmlToMarkdownQuery request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Content)) 
            return Task.FromResult(request.Content);

        try
        {
            var converter = new ReverseMarkdown.Converter();
            return Task.FromResult<string?>(converter.Convert(request.Content));
        }
        catch (Exception ex)
        {
            _logger.LogException(ex, "HTML转Markdown失败", "ConvertHtmlToMarkdownHandler.Handle");
            return Task.FromResult<string?>(request.Content);
        }
    }
}

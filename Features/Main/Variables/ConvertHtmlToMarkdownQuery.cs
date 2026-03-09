using MediatR;
using PromptMasterv6.Infrastructure.Services;
using ReverseMarkdown;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace PromptMasterv6.Features.Main.Variables
{
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
                var converter = new Converter();
                return Task.FromResult<string?>(converter.Convert(request.Content));
            }
            catch (Exception ex)
            {
                _logger.LogException(ex, "HTML转Markdown失败", "ConvertHtmlToMarkdownHandler.Handle");
                return Task.FromResult<string?>(request.Content);
            }
        }
    }
}

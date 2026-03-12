using MediatR;
using PromptMasterv6.Infrastructure.Services;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace PromptMasterv6.Features.Main.ContentEditor;

public static class SearchOnGitHubFeature
{
    public record Command(string Query) : IRequest<Result>;
    
    public record Result(bool Success, string? ErrorMessage);

    public class Handler : IRequestHandler<Command, Result>
    {
        private readonly LoggerService _logger;

        public Handler(LoggerService logger)
        {
            _logger = logger;
        }

        public Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.Query))
            {
                return Task.FromResult(new Result(false, "请输入要搜索的内容"));
            }

            try
            {
                var url = $"https://github.com/search?q={Uri.EscapeDataString(request.Query)}";

                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });

                _logger.LogInfo($"GitHub search executed: {request.Query}", "SearchOnGitHubFeature");
                
                return Task.FromResult(new Result(true, null));
            }
            catch (Exception ex)
            {
                _logger.LogException(ex, "GitHub search failed", "SearchOnGitHubFeature");
                return Task.FromResult(new Result(false, ex.Message));
            }
        }
    }
}

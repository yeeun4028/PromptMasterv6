using MediatR;
using PromptMasterv6.Infrastructure.Services;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace PromptMasterv6.Features.Workspace.SearchOnGitHub;

public static class SearchOnGitHubFeature
{
    // 1. 定义输入
    public record Command(string Query) : IRequest<Result>;

    // 2. 定义输出
    public record Result(bool Success, string? ErrorMessage);

    // 3. 执行逻辑
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
                return Task.FromResult(new Result(false, "请输入要搜索的内容。"));
            }

            try
            {
                var url = $"https://github.com/search?q={Uri.EscapeDataString(request.Query)}";

                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });

                return Task.FromResult(new Result(true, null));
            }
            catch (Exception ex)
            {
                _logger.LogException(ex, "SearchOnGitHub Failed", "SearchOnGitHubFeature.Handle");
                return Task.FromResult(new Result(false, $"打开 GitHub 失败: {ex.Message}"));
            }
        }
    }
}

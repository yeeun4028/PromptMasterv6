using MediatR;
using PromptMasterv6.Infrastructure.Services;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace PromptMasterv6.Features.Settings.Sync
{
    public static class ExportConfigFeature
    {
        public record Command(string FilePath) : IRequest<Result>;
        public record Result(bool Success, string Message);

        public class Handler : IRequestHandler<Command, Result>
        {
            private readonly SettingsService _settingsService;

            public Handler(SettingsService settingsService)
            {
                _settingsService = settingsService;
            }

            public Task<Result> Handle(Command request, CancellationToken cancellationToken)
            {
                try
                {
                    _settingsService.ExportSettings(request.FilePath);
                    return Task.FromResult(new Result(true, "配置导出成功！"));
                }
                catch (Exception ex)
                {
                    return Task.FromResult(new Result(false, $"配置导出失败: {ex.Message}"));
                }
            }
        }
    }
}

using MediatR;
using PromptMasterv6.Infrastructure.Services;
using System.Threading;
using System.Threading.Tasks;

namespace PromptMasterv6.Features.Settings.Launcher
{
    public static class AddSearchPathFeature
    {
        // 1. 定义输入
        public record Command(string Path) : IRequest<Result>;

        // 2. 定义输出
        public record Result(bool Success, string? ErrorMessage);

        // 3. 执行逻辑
        public class Handler : IRequestHandler<Command, Result>
        {
            private readonly SettingsService _settingsService;

            public Handler(SettingsService settingsService)
            {
                _settingsService = settingsService;
            }

            public Task<Result> Handle(Command request, CancellationToken cancellationToken)
            {
                var path = request.Path;
                var config = _settingsService.Config;

                if (string.IsNullOrWhiteSpace(path))
                {
                    return Task.FromResult(new Result(false, "路径不能为空"));
                }

                if (config.LauncherSearchPaths.Contains(path))
                {
                    return Task.FromResult(new Result(false, "该路径已存在"));
                }

                config.LauncherSearchPaths.Add(path);
                _settingsService.SaveConfig();

                return Task.FromResult(new Result(true, null));
            }
        }
    }
}

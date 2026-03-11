using PromptMasterv6.Infrastructure.Services;

namespace PromptMasterv6.Features.Settings.Launcher
{
    public static class AddSearchPathFeature
    {
        public record Command(string Path);

        public record Result(bool Success, string? ErrorMessage);

        public class Handler
        {
            private readonly SettingsService _settingsService;

            public Handler(SettingsService settingsService)
            {
                _settingsService = settingsService;
            }

            public Result Handle(Command request)
            {
                var path = request.Path;
                var config = _settingsService.Config;

                if (string.IsNullOrWhiteSpace(path))
                {
                    return new Result(false, "路径不能为空");
                }

                if (config.LauncherSearchPaths.Contains(path))
                {
                    return new Result(false, "该路径已存在");
                }

                config.LauncherSearchPaths.Add(path);
                _settingsService.SaveConfig();

                return new Result(true, null);
            }
        }
    }
}

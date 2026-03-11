using PromptMasterv6.Infrastructure.Services;

namespace PromptMasterv6.Features.Settings.Launcher
{
    public static class RemoveSearchPathFeature
    {
        public record Command(string Path);

        public class Handler
        {
            private readonly SettingsService _settingsService;

            public Handler(SettingsService settingsService)
            {
                _settingsService = settingsService;
            }

            public void Handle(Command request)
            {
                var path = request.Path;
                if (string.IsNullOrWhiteSpace(path)) return;

                var config = _settingsService.Config;
                config.LauncherSearchPaths.Remove(path);
                _settingsService.SaveConfig();
            }
        }
    }
}

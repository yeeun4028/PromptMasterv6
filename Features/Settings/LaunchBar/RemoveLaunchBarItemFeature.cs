using PromptMasterv6.Infrastructure.Services;
using PromptMasterv6.Features.Shared.Models;

namespace PromptMasterv6.Features.Settings.LaunchBar
{
    public static class RemoveLaunchBarItemFeature
    {
        public record Command(LaunchBarItem Item);
        public record Result(bool Success);

        public class Handler
        {
            private readonly SettingsService _settingsService;

            public Handler(SettingsService settingsService)
            {
                _settingsService = settingsService;
            }

            public Result Handle(Command request)
            {
                if (request.Item == null)
                {
                    return new Result(false);
                }

                _settingsService.Config.LaunchBarItems.Remove(request.Item);
                _settingsService.SaveConfig();
                
                return new Result(true);
            }
        }
    }
}

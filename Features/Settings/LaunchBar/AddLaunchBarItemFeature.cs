using PromptMasterv6.Infrastructure.Services;
using PromptMasterv6.Features.Shared.Models;

namespace PromptMasterv6.Features.Settings.LaunchBar
{
    public static class AddLaunchBarItemFeature
    {
        public record Command(string ColorHex, LaunchBarActionType ActionType, string ActionTarget, string Label);
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
                _settingsService.Config.LaunchBarItems.Add(new LaunchBarItem
                {
                    ColorHex = request.ColorHex,
                    ActionType = request.ActionType,
                    ActionTarget = request.ActionTarget,
                    Label = request.Label
                });
                _settingsService.SaveConfig();
                
                return new Result(true);
            }
        }
    }
}

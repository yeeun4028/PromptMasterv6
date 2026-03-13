using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using PromptMasterv6.Infrastructure.Services;

namespace PromptMasterv6.Features.Main.Hotkeys;

public static class UpdateWindowHotkeysFeature
{
    // 1. 定义输入
    public record Command(string LaunchBarHotkey) : IRequest<Result>;

    // 2. 定义输出
    public record Result(bool Success, bool NewEnableLaunchBarState, string? ErrorMessage = null);

    // 3. 执行逻辑
    public class Handler : IRequestHandler<Command, Result>
    {
        private readonly HotkeyService _hotkeyService;
        private readonly SettingsService _settingsService;
        private readonly LoggerService _logger;

        public Handler(HotkeyService hotkeyService, SettingsService settingsService, LoggerService logger)
        {
            _hotkeyService = hotkeyService;
            _settingsService = settingsService;
            _logger = logger;
        }

        public Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            try
            {
                _hotkeyService.RegisterWindowHotkey("ToggleLaunchBarHotkey", request.LaunchBarHotkey, () =>
                {
                    _settingsService.Config.EnableLaunchBar = !_settingsService.Config.EnableLaunchBar;
                });

                return Task.FromResult(new Result(true, _settingsService.Config.EnableLaunchBar));
            }
            catch (Exception ex)
            {
                _logger.LogException(ex, "Failed to update window hotkeys", "UpdateWindowHotkeysFeature.Handle");
                return Task.FromResult(new Result(false, _settingsService.Config.EnableLaunchBar, ex.Message));
            }
        }
    }
}

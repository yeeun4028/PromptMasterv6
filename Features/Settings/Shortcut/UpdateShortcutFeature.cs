using PromptMasterv6.Infrastructure.Services;

namespace PromptMasterv6.Features.Settings.Shortcut;

/// <summary>
/// 更新快捷键设置功能
/// </summary>
public static class UpdateShortcutFeature
{
    // 1. 定义输入
    public record Command(
        string FullWindowHotkey,
        string ScreenshotTranslateHotkey,
        string OcrHotkey,
        string PinToScreenHotkey
    );

    // 2. 定义输出
    public record Result(bool Success, string Message);

    // 3. 执行逻辑
    public class Handler
    {
        private readonly SettingsService _settingsService;
        private readonly LoggerService _logger;

        public Handler(SettingsService settingsService, LoggerService logger)
        {
            _settingsService = settingsService;
            _logger = logger;
        }

        public async Task<Result> Handle(Command request)
        {
            try
            {
                // 更新配置
                _settingsService.Config.FullWindowHotkey = request.FullWindowHotkey;
                _settingsService.Config.ScreenshotTranslateHotkey = request.ScreenshotTranslateHotkey;
                _settingsService.Config.OcrHotkey = request.OcrHotkey;
                _settingsService.Config.PinToScreenHotkey = request.PinToScreenHotkey;

                // 保存配置文件
                await Task.Run(() => _settingsService.SaveConfig());

                _logger.LogInfo("快捷键设置已更新", "UpdateShortcutFeature.Handle");

                return new Result(true, "快捷键设置已保存");
            }
            catch (Exception ex)
            {
                _logger.LogException(ex, "保存快捷键设置失败", "UpdateShortcutFeature.Handle");
                return new Result(false, "配置保存失败,请检查文件权限");
            }
        }
    }
}

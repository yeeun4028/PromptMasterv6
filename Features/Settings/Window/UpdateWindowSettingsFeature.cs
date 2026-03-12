using PromptMasterv6.Infrastructure.Services;

namespace PromptMasterv6.Features.Settings.Window;

/// <summary>
/// 更新窗口设置功能
/// </summary>
public static class UpdateWindowSettingsFeature
{
    // 1. 定义输入
    public record Command(bool AutoHide);

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
                _settingsService.Config.AutoHide = request.AutoHide;

                // 保存配置文件
                await Task.Run(() => _settingsService.SaveConfig());

                _logger.LogInfo("窗口设置已更新", "UpdateWindowSettingsFeature.Handle");

                return new Result(true, "窗口设置已保存");
            }
            catch (Exception ex)
            {
                _logger.LogException(ex, "保存窗口设置失败", "UpdateWindowSettingsFeature.Handle");
                return new Result(false, "配置保存失败,请检查文件权限");
            }
        }
    }
}

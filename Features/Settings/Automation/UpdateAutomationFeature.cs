using PromptMasterv6.Infrastructure.Services;

namespace PromptMasterv6.Features.Settings.Automation;

/// <summary>
/// 更新自动化设置功能
/// </summary>
public static class UpdateAutomationFeature
{
    // 1. 定义输入
    public record Command(string DefaultWebTargetName, bool EnableDoubleEnterSend);

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
                _settingsService.Config.DefaultWebTargetName = request.DefaultWebTargetName;
                _settingsService.Config.EnableDoubleEnterSend = request.EnableDoubleEnterSend;

                // 保存配置文件
                await Task.Run(() => _settingsService.SaveConfig());

                _logger.LogInfo("自动化设置已更新", "UpdateAutomationFeature.Handle");

                return new Result(true, "自动化设置已保存");
            }
            catch (Exception ex)
            {
                _logger.LogException(ex, "保存自动化设置失败", "UpdateAutomationFeature.Handle");
                return new Result(false, "配置保存失败,请检查文件权限");
            }
        }
    }
}

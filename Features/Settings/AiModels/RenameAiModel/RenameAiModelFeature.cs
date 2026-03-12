using PromptMasterv6.Infrastructure.Services;

namespace PromptMasterv6.Features.Settings.AiModels.RenameAiModel;

/// <summary>
/// 重命名AI模型功能
/// </summary>
public static class RenameAiModelFeature
{
    /// <summary>
    /// 定义输入
    /// </summary>
    public record Command(string ModelId, string NewRemark);

    /// <summary>
    /// 定义输出
    /// </summary>
    public record Result(bool Success, string Message);

    /// <summary>
    /// 执行逻辑
    /// </summary>
    public class Handler
    {
        private readonly SettingsService _settingsService;
        private readonly LoggerService _logger;

        /// <summary>
        /// 只注入当前 Feature 绝对需要的服务
        /// </summary>
        public Handler(SettingsService settingsService, LoggerService logger)
        {
            _settingsService = settingsService;
            _logger = logger;
        }

        /// <summary>
        /// 在这里实现从头到尾的业务逻辑
        /// </summary>
        public async Task<Result> Handle(Command request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.ModelId))
                {
                    return new Result(false, "模型ID不能为空");
                }

                if (string.IsNullOrWhiteSpace(request.NewRemark))
                {
                    return new Result(false, "备注不能为空");
                }

                var model = _settingsService.Config.SavedModels.FirstOrDefault(m => m.Id == request.ModelId);
                if (model == null)
                {
                    return new Result(false, "模型不存在");
                }

                var oldRemark = model.Remark;
                model.Remark = request.NewRemark;
                _settingsService.SaveConfig();

                _logger.LogInfo($"重命名模型: {model.DisplayName} (备注: {oldRemark} -> {request.NewRemark})", "RenameAiModelFeature.Handle");

                return new Result(true, "重命名成功");
            }
            catch (Exception ex)
            {
                _logger.LogError($"重命名模型失败: {ex.Message}", "RenameAiModelFeature.Handle");
                return new Result(false, $"重命名失败: {ex.Message}");
            }
        }
    }
}

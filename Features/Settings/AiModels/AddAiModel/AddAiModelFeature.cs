using PromptMasterv6.Features.Shared.Models;
using PromptMasterv6.Infrastructure.Services;

namespace PromptMasterv6.Features.Settings.AiModels.AddAiModel;

/// <summary>
/// 添加AI模型功能
/// </summary>
public static class AddAiModelFeature
{
    /// <summary>
    /// 定义输入
    /// </summary>
    public record Command(
        string ModelName = "gpt-3.5-turbo",
        string BaseUrl = "https://api.openai.com/v1",
        string ApiKey = "",
        string Remark = "New Model"
    );

    /// <summary>
    /// 定义输出
    /// </summary>
    public record Result(bool Success, string Message, AiModelConfig? AddedModel);

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
                var newModel = new AiModelConfig
                {
                    Id = Guid.NewGuid().ToString(),
                    ModelName = request.ModelName,
                    BaseUrl = request.BaseUrl,
                    ApiKey = request.ApiKey,
                    Remark = request.Remark
                };

                _settingsService.Config.SavedModels.Insert(0, newModel);

                // 如果没有活动模型,设置为活动模型
                if (string.IsNullOrEmpty(_settingsService.Config.ActiveModelId))
                {
                    _settingsService.Config.ActiveModelId = newModel.Id;
                }

                _settingsService.SaveConfig();

                _logger.LogInfo($"添加新模型: {newModel.DisplayName} (ID: {newModel.Id})", "AddAiModelFeature.Handle");

                return new Result(true, "模型添加成功", newModel);
            }
            catch (Exception ex)
            {
                _logger.LogError($"添加模型失败: {ex.Message}", "AddAiModelFeature.Handle");
                return new Result(false, $"添加失败: {ex.Message}", null);
            }
        }
    }
}

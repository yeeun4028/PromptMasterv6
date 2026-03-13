using System.Threading;
using System.Threading.Tasks;
using MediatR;
using PromptMasterv6.Infrastructure.Services;

namespace PromptMasterv6.Features.Settings.ExternalTools.HandleAiModelDeleted;

/// <summary>
/// 处理AI模型删除消息功能
/// </summary>
public static class HandleAiModelDeletedFeature
{
    /// <summary>
    /// 定义输入（必须实现 IRequest）
    /// </summary>
    public record Command(string DeletedModelName) : IRequest<Result>;

    /// <summary>
    /// 定义输出
    /// </summary>
    public record Result(bool Success, string Message, int AffectedConfigCount);

    /// <summary>
    /// 执行逻辑（必须实现 IRequestHandler）
    /// </summary>
    public class Handler : IRequestHandler<Command, Result>
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
        /// 必须带有 CancellationToken 以支持异步取消
        /// </summary>
        public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.DeletedModelName))
                {
                    return new Result(false, "模型名称不能为空", 0);
                }

                // 查找使用该模型的所有AI翻译配置
                var affectedConfigs = _settingsService.Config.SavedAiTranslationConfigs
                    .Where(c => c.Model == request.DeletedModelName)
                    .ToList();

                if (affectedConfigs.Any())
                {
                    // 将模型标记为已失效
                    foreach (var config in affectedConfigs)
                    {
                        config.Model = "已失效 (请重新配置)";
                    }

                    _settingsService.SaveConfig();
                    _logger.LogInfo($"已标记 {affectedConfigs.Count} 个使用已删除模型的AI翻译配置为失效", "HandleAiModelDeletedFeature.Handle");

                    return new Result(true, $"警告：翻译引擎依赖的模型 '{request.DeletedModelName}' 已被删除，请重新配置。", affectedConfigs.Count);
                }

                return new Result(true, "无受影响的配置", 0);
            }
            catch (Exception ex)
            {
                _logger.LogError($"处理AI模型删除消息失败: {ex.Message}", "HandleAiModelDeletedFeature.Handle");
                return new Result(false, $"处理失败: {ex.Message}", 0);
            }
        }
    }
}

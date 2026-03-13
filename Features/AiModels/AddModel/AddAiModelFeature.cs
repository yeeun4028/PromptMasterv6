using MediatR;
using PromptMasterv6.Features.Shared.Models;
using PromptMasterv6.Infrastructure.Services;
using System.Threading;
using System.Threading.Tasks;

namespace PromptMasterv6.Features.AiModels.AddModel;

public static class AddAiModelFeature
{
    public record Command(
        string ModelName = "gpt-3.5-turbo",
        string BaseUrl = "https://api.openai.com/v1",
        string ApiKey = "",
        string Remark = "New Model"
    ) : IRequest<Result>;

    public record Result(bool Success, string Message, AiModelConfig? AddedModel);

    public class Handler : IRequestHandler<Command, Result>
    {
        private readonly SettingsService _settingsService;
        private readonly LoggerService _logger;

        public Handler(SettingsService settingsService, LoggerService logger)
        {
            _settingsService = settingsService;
            _logger = logger;
        }

        public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
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

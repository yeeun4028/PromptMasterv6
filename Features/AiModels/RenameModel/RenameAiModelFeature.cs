using MediatR;
using PromptMasterv6.Infrastructure.Services;
using System.Threading;
using System.Threading.Tasks;

namespace PromptMasterv6.Features.AiModels.RenameModel;

public static class RenameAiModelFeature
{
    public record Command(string ModelId, string NewRemark) : IRequest<Result>;

    public record Result(bool Success, string Message);

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

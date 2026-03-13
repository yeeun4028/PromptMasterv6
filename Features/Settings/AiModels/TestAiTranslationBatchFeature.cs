using MediatR;
using PromptMasterv6.Features.Ai.TestConnection;
using PromptMasterv6.Features.Shared.Models;
using PromptMasterv6.Infrastructure.Services;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PromptMasterv6.Features.Settings.AiModels
{
    public static class TestAiTranslationBatchFeature
    {
        public record Command() : IRequest<Result>;

        public record Result(
            bool Success,
            string Message,
            int SuccessCount,
            int TotalCount,
            long AverageResponseTimeMs);

        public class Handler : IRequestHandler<Command, Result>
        {
            private readonly SettingsService _settingsService;
            private readonly IMediator _mediator;

            public Handler(SettingsService settingsService, IMediator mediator)
            {
                _settingsService = settingsService;
                _mediator = mediator;
            }

            public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
            {
                var enabledModels = _settingsService.Config.SavedModels
                    .Where(m => m.IsEnableForTranslation)
                    .ToList();

                if (enabledModels.Count == 0)
                {
                    return new Result(false, "请先勾选至少一个参与翻译的 AI 模型", 0, 0, 0);
                }

                int successCount = 0;
                string lastError = "";
                long totalTime = 0;

                foreach (var model in enabledModels)
                {
                    var result = await _mediator.Send(new Features.Ai.TestConnection.TestConnectionFeature.Command(
                        model.ApiKey,
                        model.BaseUrl,
                        model.ModelName,
                        model.UseProxy), cancellationToken);

                    if (result.Success)
                    {
                        successCount++;
                        if (result.ResponseTimeMs.HasValue)
                            totalTime += result.ResponseTimeMs.Value;
                    }
                    else
                    {
                        lastError = result.Message;
                    }
                }

                long avgTime = successCount > 0 ? totalTime / successCount : 0;

                if (successCount == enabledModels.Count)
                {
                    return new Result(true, $"全部 {successCount} 个模型连接成功 (平均 {avgTime}ms)", successCount, enabledModels.Count, avgTime);
                }
                else if (successCount > 0)
                {
                    return new Result(true, $"部分成功 ({successCount}/{enabledModels.Count})\n失败示例: {lastError}", successCount, enabledModels.Count, avgTime);
                }
                else
                {
                    return new Result(false, $"全部失败。\n错误示例: {lastError}", 0, enabledModels.Count, 0);
                }
            }
        }
    }
}

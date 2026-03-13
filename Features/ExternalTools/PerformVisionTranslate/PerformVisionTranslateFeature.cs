using MediatR;
using PromptMasterv6.Features.Ai.ChatWithImage;
using PromptMasterv6.Features.Shared.Models;
using PromptMasterv6.Infrastructure.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PromptMasterv6.Features.ExternalTools.PerformVisionTranslate;

public static class PerformVisionTranslateFeature
{
    public record Command(
        byte[] ImageBytes,
        List<AiModelConfig> VisionModels,
        string? VisionTranslationPromptTemplate) : IRequest<Result>;

    public record Result(bool Success, string? TranslatedText, string? ErrorMessage);

    public class Handler : IRequestHandler<Command, Result>
    {
        private readonly IMediator _mediator;
        private readonly LoggerService _logger;

        public Handler(
            IMediator mediator,
            LoggerService logger)
        {
            _mediator = mediator;
            _logger = logger;
        }

        public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            if (request.ImageBytes == null || request.ImageBytes.Length == 0)
            {
                return new Result(false, null, "图片数据为空");
            }

            if (request.VisionModels == null || request.VisionModels.Count == 0)
            {
                return new Result(false, null, "未启用 Vision 模型");
            }

            var systemPrompt = request.VisionTranslationPromptTemplate
                ?? PromptTemplates.Default.VisionTranslationSystemPrompt;

            var tasks = new List<Task<string>>();

            tasks.AddRange(request.VisionModels.Select(async model =>
            {
                try
                {
                    var result = await _mediator.Send(new Features.Ai.ChatWithImage.ChatWithImageFeature.Command(
                        request.ImageBytes,
                        model.ApiKey,
                        model.BaseUrl,
                        model.ModelName,
                        systemPrompt), cancellationToken);
                    
                    return result.Success ? result.Content : $"Vision Error: {result.ErrorMessage}";
                }
                catch (Exception ex)
                {
                    return $"Vision Error: {ex.Message}";
                }
            }));

            while (tasks.Count > 0)
            {
                var finishedTask = await Task.WhenAny(tasks).ConfigureAwait(false);
                tasks.Remove(finishedTask);

                try
                {
                    var result = await finishedTask.ConfigureAwait(false);
                    if (!string.IsNullOrWhiteSpace(result) &&
                        !result.StartsWith("Vision Error") &&
                        !result.StartsWith("错误"))
                    {
                        return new Result(true, result, null);
                    }
                }
                catch { }
            }

            return new Result(false, null, "Vision Error: 所有 Vision 模型均请求失败");
        }
    }
}

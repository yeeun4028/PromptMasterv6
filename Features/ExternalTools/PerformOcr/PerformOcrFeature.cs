using MediatR;
using PromptMasterv6.Features.Shared.Models;
using PromptMasterv6.Infrastructure.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PromptMasterv6.Features.ExternalTools.PerformOcr;

public static class PerformOcrFeature
{
    // 1. 定义输入
    public record Command(
        byte[] ImageBytes,
        List<ApiProfile> OcrProfiles,
        List<AiModelConfig> EnabledAiModels,
        string? OcrPromptTemplate) : IRequest<Result>;

    // 2. 定义输出
    public record Result(bool Success, string? OcrText, string? ErrorMessage);

    // 3. 执行逻辑
    public class Handler : IRequestHandler<Command, Result>
    {
        private readonly AiService _aiService;
        private readonly BaiduService _baiduService;
        private readonly TencentService _tencentService;
        private readonly LoggerService _logger;

        public Handler(
            AiService aiService,
            BaiduService baiduService,
            TencentService tencentService,
            LoggerService logger)
        {
            _aiService = aiService;
            _baiduService = baiduService;
            _tencentService = tencentService;
            _logger = logger;
        }

        public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            if (request.ImageBytes == null || request.ImageBytes.Length == 0)
            {
                return new Result(false, null, "图片数据为空");
            }

            var tasks = new List<Task<string>>();

            // 标准 OCR 服务
            if (request.OcrProfiles != null && request.OcrProfiles.Count > 0)
            {
                var standardProfiles = request.OcrProfiles.Where(p => p.Provider != ApiProvider.AI).ToList();
                tasks.AddRange(standardProfiles.Select(async profile =>
                {
                    try
                    {
                        return profile.Provider switch
                        {
                            ApiProvider.Baidu => await _baiduService.OcrAsync(request.ImageBytes, profile).ConfigureAwait(false),
                            ApiProvider.Tencent => await _tencentService.OcrAsync(request.ImageBytes, profile).ConfigureAwait(false),
                            _ => ""
                        };
                    }
                    catch { return ""; }
                }));
            }

            // AI OCR 服务
            bool isAiOcrEnabled = request.OcrProfiles?.Any(p => p.Provider == ApiProvider.AI && p.ServiceType == ServiceType.OCR) ?? false;

            if (isAiOcrEnabled && request.EnabledAiModels != null && request.EnabledAiModels.Any())
            {
                var ocrPrompt = request.OcrPromptTemplate ?? PromptTemplates.Default.OcrSystemPrompt;

                tasks.AddRange(request.EnabledAiModels.Select(async model =>
                {
                    try
                    {
                        return await _aiService.ChatWithImageAsync(
                            request.ImageBytes,
                            model.ApiKey,
                            model.BaseUrl,
                            model.ModelName,
                            ocrPrompt).ConfigureAwait(false);
                    }
                    catch { return ""; }
                }));
            }

            if (tasks.Count == 0)
            {
                return new Result(false, null, "未启用任何 OCR 服务 (Standard or AI), 或者开启了 AI OCR 但未选择模型");
            }

            // 竞速执行
            while (tasks.Count > 0)
            {
                var finishedTask = await Task.WhenAny(tasks).ConfigureAwait(false);
                tasks.Remove(finishedTask);

                try
                {
                    var result = await finishedTask.ConfigureAwait(false);
                    if (!string.IsNullOrWhiteSpace(result) &&
                        !result.StartsWith("OCR 错误") &&
                        !result.StartsWith("错误"))
                    {
                        return new Result(true, result, null);
                    }
                }
                catch { }
            }

            return new Result(false, null, "OCR 失败：所有服务均未能识别文字");
        }
    }
}

using MediatR;
using PromptMasterv6.Features.Shared.Models;
using PromptMasterv6.Infrastructure.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PromptMasterv6.Features.ExternalTools.PerformTranslate;

public static class PerformTranslateFeature
{
    // 1. 定义输入
    public record Command(
        string Text,
        List<ApiProfile> TranslateProfiles,
        List<AiModelConfig> EnabledAiModels,
        string? TranslationPromptTemplate,
        string? AiTranslationPromptContent) : IRequest<Result>;

    // 2. 定义输出
    public record Result(bool Success, string? TranslatedText, string? ErrorMessage);

    // 3. 执行逻辑
    public class Handler : IRequestHandler<Command, Result>
    {
        private readonly AiService _aiService;
        private readonly BaiduService _baiduService;
        private readonly TencentService _tencentService;
        private readonly GoogleService _googleService;
        private readonly LoggerService _logger;

        public Handler(
            AiService aiService,
            BaiduService baiduService,
            TencentService tencentService,
            GoogleService googleService,
            LoggerService logger)
        {
            _aiService = aiService;
            _baiduService = baiduService;
            _tencentService = tencentService;
            _googleService = googleService;
            _logger = logger;
        }

        public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.Text))
            {
                return new Result(false, null, "待翻译文本为空");
            }

            var tasks = new List<Task<string>>();

            // 标准翻译服务
            if (request.TranslateProfiles != null && request.TranslateProfiles.Count > 0)
            {
                tasks.AddRange(request.TranslateProfiles.Select(async profile =>
                {
                    try
                    {
                        return profile.Provider switch
                        {
                            ApiProvider.Google => await _googleService.TranslateAsync(request.Text, profile).ConfigureAwait(false),
                            ApiProvider.Baidu => await _baiduService.TranslateAsync(request.Text, profile, "auto", "zh").ConfigureAwait(false),
                            ApiProvider.Tencent => await _tencentService.TranslateAsync(request.Text, profile, "auto", "zh").ConfigureAwait(false),
                            _ => ""
                        };
                    }
                    catch { return ""; }
                }));
            }

            // AI 翻译服务
            if (request.EnabledAiModels != null && request.EnabledAiModels.Any())
            {
                var systemPrompt = request.AiTranslationPromptContent
                    ?? request.TranslationPromptTemplate
                    ?? PromptTemplates.Default.TranslationSystemPrompt;

                tasks.AddRange(request.EnabledAiModels.Select(async model =>
                {
                    try
                    {
                        if (string.IsNullOrWhiteSpace(model.ApiKey)) return "";

                        return await _aiService.ChatAsync(
                            request.Text,
                            model.ApiKey,
                            model.BaseUrl,
                            model.ModelName,
                            systemPrompt,
                            model.UseProxy).ConfigureAwait(false) ?? "";
                    }
                    catch { return ""; }
                }));
            }

            if (tasks.Count == 0)
            {
                return new Result(false, null, "未启用任何翻译服务 (Standard or AI)");
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
                        !result.StartsWith("翻译失败") &&
                        !result.StartsWith("错误") &&
                        !result.StartsWith("AI 翻译失败") &&
                        !result.StartsWith("[AI 错误]") &&
                        !result.StartsWith("[系统错误]"))
                    {
                        return new Result(true, result, null);
                    }
                }
                catch { }
            }

            return new Result(false, null, "翻译失败：所有服务均无响应");
        }
    }
}

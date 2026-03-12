using MediatR;
using PromptMasterv6.Features.Shared.Models;
using PromptMasterv6.Features.Main.FileManager.Messages;
using PromptMasterv6.Infrastructure.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PromptMasterv6.Features.ExternalTools.PerformScreenshotTranslate;

public static class PerformScreenshotTranslateFeature
{
    // 1. 定义输入
    public record Command() : IRequest<Result>;

    // 2. 定义输出
    public record Result(
        bool Success,
        string? TranslatedText,
        System.Windows.Rect? ActionRect,
        bool ShouldOpenSettings,
        bool ShouldShowPopup,
        string? ErrorMessage);

    // 3. 执行逻辑
    public class Handler : IRequestHandler<Command, Result>
    {
        private readonly SettingsService _settingsService;
        private readonly WindowManager _windowManager;
        private readonly IMediator _mediator;
        private readonly LoggerService _logger;

        public Handler(
            SettingsService settingsService,
            WindowManager windowManager,
            IMediator mediator,
            LoggerService logger)
        {
            _settingsService = settingsService;
            _windowManager = windowManager;
            _mediator = mediator;
            _logger = logger;
        }

        public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            var config = _settingsService.Config;

            // 获取 Vision 模型
            var enabledVisionModels = config.SavedModels
                .Where(m => m.IsEnableForScreenshotTranslate)
                .ToList();
            bool useVisionTranslate = enabledVisionModels.Any();

            // 获取 OCR 和翻译 Profile
            var enabledOcrProfiles = config.ApiProfiles
                .Where(p => p.ServiceType == ServiceType.OCR && p.IsEnabled)
                .ToList();
            var enabledTransProfiles = config.ApiProfiles
                .Where(p => p.ServiceType == ServiceType.Translation && p.IsEnabled)
                .ToList();

            // 检查配置
            if (!useVisionTranslate && (enabledOcrProfiles.Count == 0 || enabledTransProfiles.Count == 0))
            {
                return new Result(false, null, null, true, false, "未配置 OCR 或翻译服务");
            }

            string? translatedResult = null;
            System.Windows.Rect? actionRect = null;

            // 获取 AI 翻译 Prompt
            string? aiTranslationPromptContent = null;
            if (!string.IsNullOrWhiteSpace(config.AiTranslationPromptId))
            {
                var msg = new RequestPromptFileMessage { PromptId = config.AiTranslationPromptId };
                CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger.Default.Send(msg, Guid.NewGuid());
                if (msg.HasReceivedResponse && msg.Response?.File?.Content != null)
                {
                    aiTranslationPromptContent = msg.Response.File.Content;
                }
            }

            // 获取 AI OCR 模型
            var enabledAiOcrModels = config.SavedModels
                .Where(m => m.IsEnableForOcr)
                .ToList();

            // 获取 AI 翻译模型
            var enabledAiTransModels = config.SavedModels
                .Where(m => m.IsEnableForTranslation)
                .ToList();

            // 显示截图窗口并执行翻译
            var capturedBytes = await _windowManager.ShowCaptureWindowAsync(async (byte[] bytes, System.Windows.Rect rect) =>
            {
                actionRect = rect;

                // 尝试 Vision 翻译
                if (useVisionTranslate)
                {
                    var visionResult = await _mediator.Send(new PerformVisionTranslate.PerformVisionTranslateFeature.Command(
                        bytes,
                        enabledVisionModels,
                        config.VisionTranslationPromptTemplate));

                    if (visionResult.Success && !string.IsNullOrWhiteSpace(visionResult.TranslatedText))
                    {
                        translatedResult = visionResult.TranslatedText;
                        return;
                    }

                    // Vision 翻译失败，检查是否有备选方案
                    if (enabledOcrProfiles.Count == 0 || enabledTransProfiles.Count == 0)
                    {
                        translatedResult = visionResult.ErrorMessage ?? "Vision 翻译失败";
                        return;
                    }
                }

                // 执行 OCR
                var ocrResult = await _mediator.Send(new PerformOcr.PerformOcrFeature.Command(
                    bytes,
                    enabledOcrProfiles,
                    enabledAiOcrModels,
                    config.OcrPromptTemplate));

                if (!ocrResult.Success || string.IsNullOrWhiteSpace(ocrResult.OcrText))
                {
                    translatedResult = ocrResult.ErrorMessage ?? "无法识别文字";
                    return;
                }

                // 执行翻译
                var transResult = await _mediator.Send(new PerformTranslate.PerformTranslateFeature.Command(
                    ocrResult.OcrText,
                    enabledTransProfiles,
                    enabledAiTransModels,
                    config.TranslationPromptTemplate,
                    aiTranslationPromptContent));

                translatedResult = transResult.Success
                    ? transResult.TranslatedText
                    : transResult.ErrorMessage;
            });

            if (capturedBytes == null)
            {
                return new Result(false, null, null, false, false, "用户取消截图");
            }

            // 判断结果
            if (string.IsNullOrWhiteSpace(translatedResult))
            {
                return new Result(false, null, actionRect, false, true, "翻译结果为空");
            }

            // 检查是否为错误结果
            bool isError = translatedResult.StartsWith("OCR 错误") ||
                           translatedResult.StartsWith("错误") ||
                           translatedResult == "无法识别文字" ||
                           translatedResult.StartsWith("Vision Error") ||
                           translatedResult.StartsWith("未启用") ||
                           translatedResult.StartsWith("翻译失败");

            if (isError)
            {
                return new Result(false, translatedResult, actionRect, false, false, translatedResult);
            }

            return new Result(true, translatedResult, actionRect, false, false, null);
        }
    }
}

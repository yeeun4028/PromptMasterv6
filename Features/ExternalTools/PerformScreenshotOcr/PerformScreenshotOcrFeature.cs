using MediatR;
using PromptMasterv6.Features.Shared.Models;
using PromptMasterv6.Infrastructure.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PromptMasterv6.Features.ExternalTools.PerformScreenshotOcr;

public static class PerformScreenshotOcrFeature
{
    // 1. 定义输入
    public record Command() : IRequest<Result>;

    // 2. 定义输出
    public record Result(bool Success, string? OcrText, bool ShouldOpenSettings, string? ErrorMessage);

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
            // 获取启用的 OCR Profile
            var enabledOcrProfiles = _settingsService.Config.ApiProfiles
                .Where(p => p.ServiceType == ServiceType.OCR && p.IsEnabled)
                .ToList();

            if (enabledOcrProfiles.Count == 0)
            {
                return new Result(false, null, true, "未配置 OCR 服务");
            }

            // 获取启用的 AI 模型
            var enabledAiModels = _settingsService.Config.SavedModels
                .Where(m => m.IsEnableForOcr)
                .ToList();

            string? ocrResult = null;

            // 显示截图窗口并执行 OCR
            var capturedBytes = await _windowManager.ShowCaptureWindowAsync(async (byte[] bytes, System.Windows.Rect rect) =>
            {
                var result = await _mediator.Send(new PerformOcr.PerformOcrFeature.Command(
                    bytes,
                    enabledOcrProfiles,
                    enabledAiModels,
                    _settingsService.Config.OcrPromptTemplate));

                ocrResult = result.Success ? result.OcrText : result.ErrorMessage;
            });

            if (capturedBytes == null)
            {
                return new Result(false, null, false, "用户取消截图");
            }

            if (string.IsNullOrWhiteSpace(ocrResult))
            {
                return new Result(false, null, false, "OCR 结果为空");
            }

            // 检查是否为错误结果
            if (ocrResult.StartsWith("OCR 错误") ||
                ocrResult.StartsWith("错误") ||
                ocrResult.StartsWith("OCR 失败") ||
                ocrResult.StartsWith("未启用"))
            {
                return new Result(false, ocrResult, false, ocrResult);
            }

            return new Result(true, ocrResult, false, null);
        }
    }
}

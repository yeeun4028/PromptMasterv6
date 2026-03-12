using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using MediatR;
using PromptMasterv6.Features.PinToScreen;
using PromptMasterv6.Infrastructure.Services;

namespace PromptMasterv6.Features.Main.Tray;

public static class PinToScreenFromCaptureFeature
{
    // 1. 定义输入
    public record Command() : IRequest<Result>;

    // 2. 定义输出
    public record Result(bool Success, string? ErrorMessage = null);

    // 3. 执行逻辑
    public class Handler : IRequestHandler<Command, Result>
    {
        private readonly WindowManager _windowManager;
        private readonly LoggerService _logger;

        public Handler(WindowManager windowManager, LoggerService logger)
        {
            _windowManager = windowManager;
            _logger = logger;
        }

        public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            try
            {
                byte[]? capturedBytes = null;
                System.Windows.Rect captureRect = default;

                capturedBytes = await _windowManager.ShowCaptureWindowAsync((bytes, rect) =>
                {
                    capturedBytes = bytes;
                    captureRect = rect;
                    return Task.CompletedTask;
                });

                if (capturedBytes == null)
                {
                    return new Result(false, "截图取消");
                }

                var bitmapSource = ByteArrayToBitmapSource(capturedBytes);
                PinToScreenWindow.PinToScreenAsync(bitmapSource);

                return new Result(true);
            }
            catch (Exception ex)
            {
                _logger.LogError($"贴图截图失败: {ex.Message}", "PinToScreenFromCaptureFeature");
                return new Result(false, ex.Message);
            }
        }

        private BitmapSource ByteArrayToBitmapSource(byte[] bytes)
        {
            using var stream = new System.IO.MemoryStream(bytes);
            var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            var bitmapSource = decoder.Frames[0];
            bitmapSource.Freeze();
            return bitmapSource;
        }
    }
}

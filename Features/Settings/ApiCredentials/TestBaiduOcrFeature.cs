using MediatR;
using PromptMasterv6.Infrastructure.Services;
using PromptMasterv6.Features.Shared.Models;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;

namespace PromptMasterv6.Features.Settings.ApiCredentials
{
    public static class TestBaiduOcrFeature
    {
        /// <summary>
        /// 定义输入（必须实现 IRequest）
        /// </summary>
        public record Command(string ApiKey, string SecretKey) : IRequest<Result>;
        
        /// <summary>
        /// 定义输出
        /// </summary>
        public record Result(bool Success, string Message);

        /// <summary>
        /// 执行逻辑（必须实现 IRequestHandler）
        /// </summary>
        public class Handler : IRequestHandler<Command, Result>
        {
            private readonly BaiduService _baiduService;
            private readonly LoggerService _logger;

            public Handler(BaiduService baiduService, LoggerService logger)
            {
                _baiduService = baiduService;
                _logger = logger;
            }

            /// <summary>
            /// 必须带有 CancellationToken 以支持异步取消
            /// </summary>
            public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
            {
                if (string.IsNullOrWhiteSpace(request.ApiKey) || string.IsNullOrWhiteSpace(request.SecretKey))
                {
                    return new Result(false, "请先填写 API Key 和 Secret Key");
                }

                try
                {
                    var profile = new ApiProfile
                    {
                        Provider = ApiProvider.Baidu,
                        ServiceType = ServiceType.OCR,
                        Key1 = request.ApiKey,
                        Key2 = request.SecretKey
                    };

                    byte[] testImage = CreateTestImage();
                    var result = await _baiduService.OcrAsync(testImage, profile);

                    if (result.StartsWith("错误") || result.Contains("错误"))
                    {
                        return new Result(false, $"连接失败：{result}");
                    }

                    return new Result(true, "连接成功！");
                }
                catch (Exception ex)
                {
                    _logger.LogException(ex, "Failed to test Baidu OCR", "TestBaiduOcrFeature.Handle");
                    return new Result(false, $"测试出错: {ex.Message}");
                }
            }

            private static byte[] CreateTestImage()
            {
                var width = 200;
                var height = 60;
                var renderBitmap = new RenderTargetBitmap(width, height, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
                var visual = new System.Windows.Media.DrawingVisual();

                using (var context = visual.RenderOpen())
                {
                    context.DrawRectangle(System.Windows.Media.Brushes.White, null, new Rect(0, 0, width, height));

                    var formattedText = new System.Windows.Media.FormattedText(
                        "OCR TEST",
                        System.Globalization.CultureInfo.InvariantCulture,
                        System.Windows.FlowDirection.LeftToRight,
                        new System.Windows.Media.Typeface("Arial"),
                        24,
                        System.Windows.Media.Brushes.Black,
                        1.0);

                    context.DrawText(formattedText, new System.Windows.Point(40, 15));
                }

                renderBitmap.Render(visual);

                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(renderBitmap));

                using (var stream = new MemoryStream())
                {
                    encoder.Save(stream);
                    return stream.ToArray();
                }
            }
        }
    }
}

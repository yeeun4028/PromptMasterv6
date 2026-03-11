using PromptMasterv6.Infrastructure.Services;
using PromptMasterv6.Features.Shared.Models;
using System;
using System.Threading.Tasks;

namespace PromptMasterv6.Features.Settings.ApiCredentials
{
    public static class TestBaiduTranslateFeature
    {
        public record Command(string AppId, string SecretKey);
        public record Result(bool Success, string Message);

        public class Handler
        {
            private readonly BaiduService _baiduService;
            private readonly LoggerService _logger;

            public Handler(BaiduService baiduService, LoggerService logger)
            {
                _baiduService = baiduService;
                _logger = logger;
            }

            public async Task<Result> Handle(Command request)
            {
                if (string.IsNullOrWhiteSpace(request.AppId) || string.IsNullOrWhiteSpace(request.SecretKey))
                {
                    return new Result(false, "请先填写 App ID 和 Secret Key");
                }

                try
                {
                    var profile = new ApiProfile
                    {
                        Provider = ApiProvider.Baidu,
                        ServiceType = ServiceType.Translation,
                        Key1 = request.AppId,
                        Key2 = request.SecretKey
                    };

                    var result = await _baiduService.TranslateAsync("Hello", profile, "en", "zh");

                    if (result.StartsWith("错误") || result.Contains("错误") || result.Contains("异常"))
                    {
                        return new Result(false, $"连接失败：{result}");
                    }

                    return new Result(true, $"连接成功！翻译结果：{result}");
                }
                catch (Exception ex)
                {
                    _logger.LogException(ex, "Failed to test Baidu Translate", "TestBaiduTranslateFeature.Handle");
                    return new Result(false, $"测试出错: {ex.Message}");
                }
            }
        }
    }
}

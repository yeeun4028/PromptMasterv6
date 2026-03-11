using PromptMasterv6.Infrastructure.Services;
using PromptMasterv6.Features.Shared.Models;
using System;
using System.Threading.Tasks;

namespace PromptMasterv6.Features.Settings.ApiCredentials
{
    public static class TestTencentTranslateFeature
    {
        public record Command(string SecretId, string SecretKey);
        public record Result(bool Success, string Message);

        public class Handler
        {
            private readonly TencentService _tencentService;
            private readonly LoggerService _logger;

            public Handler(TencentService tencentService, LoggerService logger)
            {
                _tencentService = tencentService;
                _logger = logger;
            }

            public async Task<Result> Handle(Command request)
            {
                if (string.IsNullOrWhiteSpace(request.SecretId) || string.IsNullOrWhiteSpace(request.SecretKey))
                {
                    return new Result(false, "请先填写 Secret ID 和 Secret Key");
                }

                try
                {
                    var profile = new ApiProfile
                    {
                        Provider = ApiProvider.Tencent,
                        ServiceType = ServiceType.Translation,
                        Key1 = request.SecretId,
                        Key2 = request.SecretKey
                    };

                    var result = await _tencentService.TranslateAsync("Hello", profile, "auto", "zh");

                    if (result.StartsWith("Error") || result.StartsWith("Tencent Error"))
                    {
                        return new Result(false, $"连接失败：{result}");
                    }

                    return new Result(true, $"连接成功！翻译结果：{result}");
                }
                catch (Exception ex)
                {
                    _logger.LogException(ex, "Failed to test Tencent Translate", "TestTencentTranslateFeature.Handle");
                    return new Result(false, $"测试出错: {ex.Message}");
                }
            }
        }
    }
}

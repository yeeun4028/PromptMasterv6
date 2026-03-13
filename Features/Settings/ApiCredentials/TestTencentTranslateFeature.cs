using MediatR;
using PromptMasterv6.Infrastructure.Services;
using PromptMasterv6.Features.Shared.Models;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace PromptMasterv6.Features.Settings.ApiCredentials
{
    public static class TestTencentTranslateFeature
    {
        /// <summary>
        /// 定义输入（必须实现 IRequest）
        /// </summary>
        public record Command(string SecretId, string SecretKey) : IRequest<Result>;
        
        /// <summary>
        /// 定义输出
        /// </summary>
        public record Result(bool Success, string Message);

        /// <summary>
        /// 执行逻辑（必须实现 IRequestHandler）
        /// </summary>
        public class Handler : IRequestHandler<Command, Result>
        {
            private readonly TencentService _tencentService;
            private readonly LoggerService _logger;

            public Handler(TencentService tencentService, LoggerService logger)
            {
                _tencentService = tencentService;
                _logger = logger;
            }

            /// <summary>
            /// 必须带有 CancellationToken 以支持异步取消
            /// </summary>
            public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
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

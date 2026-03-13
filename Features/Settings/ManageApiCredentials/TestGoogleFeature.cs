using MediatR;
using PromptMasterv6.Infrastructure.Services;
using PromptMasterv6.Features.Shared.Models;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace PromptMasterv6.Features.Settings.ApiCredentials
{
    public static class TestGoogleFeature
    {
        /// <summary>
        /// 定义输入（必须实现 IRequest）
        /// </summary>
        public record Command(string BaseUrl, string ApiKey) : IRequest<Result>;
        
        /// <summary>
        /// 定义输出
        /// </summary>
        public record Result(bool Success, string Message);

        /// <summary>
        /// 执行逻辑（必须实现 IRequestHandler）
        /// </summary>
        public class Handler : IRequestHandler<Command, Result>
        {
            private readonly GoogleService _googleService;
            private readonly LoggerService _logger;

            public Handler(GoogleService googleService, LoggerService logger)
            {
                _googleService = googleService;
                _logger = logger;
            }

            /// <summary>
            /// 必须带有 CancellationToken 以支持异步取消
            /// </summary>
            public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
            {
                if (string.IsNullOrWhiteSpace(request.ApiKey))
                {
                    return new Result(false, "请先填写 API Key");
                }

                try
                {
                    var profile = new ApiProfile
                    {
                        Provider = ApiProvider.Google,
                        ServiceType = ServiceType.Translation,
                        BaseUrl = request.BaseUrl ?? "",
                        Key1 = request.ApiKey
                    };

                    var result = await _googleService.TranslateAsync("Hello World", profile);

                    if (!string.IsNullOrWhiteSpace(result) && !result.StartsWith("Google") && !result.StartsWith("错误") && !result.StartsWith("Google API 错误"))
                    {
                        return new Result(true, $"连接成功！翻译结果：{result}");
                    }

                    return new Result(false, $"连接失败：{result}");
                }
                catch (Exception ex)
                {
                    _logger.LogException(ex, "Failed to test Google", "TestGoogleFeature.Handle");
                    return new Result(false, $"测试出错: {ex.Message}");
                }
            }
        }
    }
}

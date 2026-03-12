using MediatR;
using PromptMasterv6.Infrastructure.Services;

namespace PromptMasterv6.Features.Settings.Proxy;

/// <summary>
/// 更新代理配置功能
/// </summary>
public static class UpdateProxyFeature
{
    // 1. 定义输入
    public record Command(string ProxyAddress) : IRequest<Result>;

    // 2. 定义输出
    public record Result(bool Success, string Message, string? ValidationError = null);

    // 3. 执行逻辑
    public class Handler : IRequestHandler<Command, Result>
    {
        private readonly SettingsService _settingsService;
        private readonly LoggerService _logger;

        public Handler(SettingsService settingsService, LoggerService logger)
        {
            _settingsService = settingsService;
            _logger = logger;
        }

        public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            // 验证代理地址格式
            if (!string.IsNullOrWhiteSpace(request.ProxyAddress))
            {
                // 支持多种代理地址格式
                // 1. 完整URL格式: http://127.0.0.1:10808
                // 2. IP:端口格式: 127.0.0.1:10808
                // 3. socks5://格式

                string addressToValidate = request.ProxyAddress.Trim();

                // 如果不包含协议前缀,尝试添加http://
                if (!addressToValidate.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                    !addressToValidate.StartsWith("https://", StringComparison.OrdinalIgnoreCase) &&
                    !addressToValidate.StartsWith("socks5://", StringComparison.OrdinalIgnoreCase) &&
                    !addressToValidate.StartsWith("socks4://", StringComparison.OrdinalIgnoreCase))
                {
                    addressToValidate = "http://" + addressToValidate;
                }

                if (!Uri.TryCreate(addressToValidate, UriKind.Absolute, out Uri? uri))
                {
                    return new Result(false, "代理地址格式不正确", "InvalidProxyAddress");
                }

                // 验证端口号
                if (uri.Port < 0 || uri.Port > 65535)
                {
                    return new Result(false, "端口号必须在 0-65535 之间", "InvalidPort");
                }
            }

            try
            {
                // 更新配置
                _settingsService.Config.ProxyAddress = request.ProxyAddress;

                // 保存配置文件
                await Task.Run(() => _settingsService.SaveConfig());

                _logger.LogInfo("代理配置已更新", "UpdateProxyFeature.Handle");

                return new Result(true, "代理配置已保存");
            }
            catch (Exception ex)
            {
                _logger.LogException(ex, "保存代理配置失败", "UpdateProxyFeature.Handle");
                return new Result(false, "配置保存失败,请检查文件权限");
            }
        }
    }
}

using PromptMasterv6.Infrastructure.Services;
using PromptMasterv6.Infrastructure.WindowRegistration;

namespace PromptMasterv6.Features.PinToScreen
{
    /// <summary>
    /// 固定到屏幕窗口注册器
    /// </summary>
    public class PinToScreenRegistrar : IWindowRegistrar
    {
        public void Register(WindowRegistry registry)
        {
            registry.RegisterPinToScreen(PinToScreenWindow.PinToScreenAsync);
        }
    }
}

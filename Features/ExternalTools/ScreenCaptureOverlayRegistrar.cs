using PromptMasterv6.Infrastructure.Services;
using PromptMasterv6.Infrastructure.WindowRegistration;

namespace PromptMasterv6.Features.ExternalTools
{
    /// <summary>
    /// 屏幕截图覆盖层窗口注册器
    /// </summary>
    public class ScreenCaptureOverlayRegistrar : IWindowRegistrar
    {
        public void Register(WindowRegistry registry)
        {
            registry.RegisterScreenCaptureOverlay((screenBitmap, onCaptureProcessing) =>
                new ScreenCaptureOverlay(screenBitmap, onCaptureProcessing));
        }
    }
}

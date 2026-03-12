using PromptMasterv6.Infrastructure.Services;
using PromptMasterv6.Infrastructure.WindowRegistration;
using PromptMasterv6.Features.Settings;

namespace PromptMasterv6.Features.Settings
{
    /// <summary>
    /// 设置窗口注册器
    /// </summary>
    public class SettingsWindowRegistrar : IWindowRegistrar
    {
        public void Register(WindowRegistry registry)
        {
            registry.RegisterWindow<SettingsViewModel, SettingsWindow>();
        }
    }
}

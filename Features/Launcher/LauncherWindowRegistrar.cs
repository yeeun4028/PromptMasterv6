using PromptMasterv6.Infrastructure.Services;
using PromptMasterv6.Infrastructure.WindowRegistration;
using PromptMasterv6.Features.Launcher;

namespace PromptMasterv6.Features.Launcher
{
    /// <summary>
    /// 启动器窗口注册器
    /// </summary>
    public class LauncherWindowRegistrar : IWindowRegistrar
    {
        public void Register(WindowRegistry registry)
        {
            registry.RegisterWindow<LauncherViewModel, LauncherWindow>();
        }
    }
}

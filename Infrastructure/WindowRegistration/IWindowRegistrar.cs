using PromptMasterv6.Infrastructure.Services;

namespace PromptMasterv6.Infrastructure.WindowRegistration
{
    /// <summary>
    /// 窗口注册器接口
    /// 用于将窗口注册逻辑分散到各Feature模块
    /// </summary>
    public interface IWindowRegistrar
    {
        /// <summary>
        /// 注册窗口到窗口注册表
        /// </summary>
        /// <param name="registry">窗口注册表</param>
        void Register(WindowRegistry registry);
    }
}

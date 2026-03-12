using Microsoft.Extensions.DependencyInjection;

namespace PromptMasterv6.Infrastructure.ServiceRegistration
{
    /// <summary>
    /// 服务模块接口
    /// 用于将服务注册按功能模块分组
    /// </summary>
    public interface IServiceModule
    {
        /// <summary>
        /// 注册当前模块的服务
        /// </summary>
        /// <param name="services">服务集合</param>
        void RegisterServices(IServiceCollection services);
    }
}

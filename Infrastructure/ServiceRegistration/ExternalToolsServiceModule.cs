using Microsoft.Extensions.DependencyInjection;
using PromptMasterv6.Features.ExternalTools;
using PromptMasterv6.Infrastructure.Services;

namespace PromptMasterv6.Infrastructure.ServiceRegistration
{
    /// <summary>
    /// 外部工具服务模块
    /// 注册外部工具相关的ViewModel和Feature Handlers
    /// </summary>
    public class ExternalToolsServiceModule : IServiceModule
    {
        public void RegisterServices(IServiceCollection services)
        {
            // ViewModels
            services.AddSingleton<ExternalToolsViewModel>();

            // HTTP Clients for External Services
            services.AddHttpClient<BaiduService>();
            services.AddHttpClient<GoogleService>();
            services.AddHttpClient<TencentService>();

            // ExternalTools Features
            services.AddSingleton<Features.ExternalTools.PerformOcr.PerformOcrFeature.Handler>();
            services.AddSingleton<Features.ExternalTools.PerformScreenshotOcr.PerformScreenshotOcrFeature.Handler>();
            services.AddSingleton<Features.ExternalTools.PerformTranslate.PerformTranslateFeature.Handler>();
            services.AddSingleton<Features.ExternalTools.PerformVisionTranslate.PerformVisionTranslateFeature.Handler>();
            services.AddSingleton<Features.ExternalTools.PerformScreenshotTranslate.PerformScreenshotTranslateFeature.Handler>();
            services.AddSingleton<Features.ExternalTools.EnsureAiProfile.EnsureAiProfileFeature.Handler>();
        }
    }
}

using Microsoft.Extensions.DependencyInjection;
using MediatR;
using PromptMasterv6.Infrastructure.Services;
using PromptMasterv6.Core.Interfaces;
using PromptMasterv6.Features.Shared.Behaviors;
using PromptMasterv6.Features.Main.Tray;

namespace PromptMasterv6.Infrastructure.ServiceRegistration
{
    /// <summary>
    /// 应用程序基础设施服务模块
    /// 注册日志、MediatR、配置、窗口注册表等基础服务
    /// </summary>
    public class ApplicationServiceModule : IServiceModule
    {
        public void RegisterServices(IServiceCollection services)
        {
            // 日志服务
            services.AddSingleton<LoggerService>(sp => LoggerService.Instance);

            // MediatR
            services.AddMediatR(cfg =>
            {
                cfg.RegisterServicesFromAssembly(typeof(App).Assembly);
                cfg.AddOpenBehavior(typeof(UnhandledExceptionBehavior<,>));
            });

            // Application Features
            services.AddSingleton<Features.AppCore.UI.ConfigureTextBoxContextMenuFeature.Handler>();
            services.AddSingleton<Features.AppCore.SingleInstance.EnsureSingleInstanceFeature.Handler>();
            services.AddSingleton<Features.AppCore.SingleInstance.ReleaseSingleInstanceFeature.Handler>();
            services.AddSingleton<Features.AppCore.ExceptionHandling.HandleUnhandledExceptionFeature.Handler>();
            services.AddSingleton<Features.AppCore.Initialization.InitializeApplicationFeature.Handler>();
            services.AddSingleton<Features.AppCore.Shutdown.CleanupApplicationFeature.Handler>();

            // 会话状态
            services.AddSingleton<ISessionState, SessionState>();

            // HTTP 客户端
            services.AddTransient<ZhipuCompatHandler>();
            services.AddHttpClient("AiServiceClient")
                .AddHttpMessageHandler<ZhipuCompatHandler>()
                .SetHandlerLifetime(TimeSpan.FromMinutes(5));
            
            services.AddHttpClient("NativeAiClient")
                .SetHandlerLifetime(TimeSpan.FromMinutes(5));

            // 配置服务
            services.AddSingleton<SettingsService>();
            services.AddSingleton<AppConfig>(sp => sp.GetRequiredService<SettingsService>().Config);

            // 窗口注册表
            services.AddSingleton<WindowRegistry>();

            // AI 服务
            services.AddSingleton<AiService>();

            // 数据服务
            services.AddSingleton<FileDataService>();
            services.AddSingleton<WebDavDataService>();
            services.AddKeyedSingleton<IDataService>("cloud", (sp, key) => sp.GetRequiredService<WebDavDataService>());
            services.AddKeyedSingleton<IDataService>("local", (sp, key) => sp.GetRequiredService<FileDataService>());
            services.AddSingleton<IDataService>(sp => sp.GetRequiredService<WebDavDataService>());

            // 全局快捷键服务
            services.AddSingleton<GlobalKeyService>();
            services.AddSingleton<HotkeyService>();
            services.AddSingleton<GlobalShortcutCoordinator>();

            // 对话框和窗口管理
            services.AddSingleton<DialogService>();
            services.AddSingleton<WindowManager>();

            // 剪贴板服务
            services.AddSingleton<ClipboardService>();

            // 托盘服务
            services.AddSingleton<TrayService>();
        }
    }
}

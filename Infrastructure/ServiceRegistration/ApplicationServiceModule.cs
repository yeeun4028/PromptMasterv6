using Microsoft.Extensions.DependencyInjection;
using MediatR;
using PromptMasterv6.Infrastructure.Services;
using PromptMasterv6.Core.Interfaces;
using PromptMasterv6.Features.Shared.Behaviors;
using PromptMasterv6.Features.Shared.Dialogs;
using PromptMasterv6.Features.Main.Tray;
using PromptMasterv6.Infrastructure.WindowRegistration;
using PromptMasterv6.Features.Launcher;
using PromptMasterv6.Features.Settings;
using PromptMasterv6.Features.ExternalTools;
using PromptMasterv6.Features.PinToScreen;
using PromptMasterv6.Infrastructure.MediatR;
using PromptMasterv6.Features.Ai;
using PromptMasterv6.Features.AppCore.Initialization;

namespace PromptMasterv6.Infrastructure.ServiceRegistration
{
    public class ApplicationServiceModule : IServiceModule
    {
        public void RegisterServices(IServiceCollection services)
        {
            services.AddSingleton<LoggerService>(sp => LoggerService.Instance);

            services.AddMediatR(cfg =>
            {
                cfg.RegisterServicesFromAssembly(typeof(App).Assembly);
                cfg.AddOpenBehavior(typeof(UnhandledExceptionBehavior<,>));
                cfg.AddOpenBehavior(typeof(ExceptionHandlingBehavior<,>));
                cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
            });

            services.AddSingleton<Features.AppCore.UI.ConfigureTextBoxContextMenuFeature.Handler>();
            services.AddSingleton<Features.AppCore.SingleInstance.EnsureSingleInstanceFeature.Handler>();
            services.AddSingleton<Features.AppCore.SingleInstance.ReleaseSingleInstanceFeature.Handler>();
            services.AddSingleton<Features.AppCore.ExceptionHandling.HandleUnhandledExceptionFeature.Handler>();
            services.AddSingleton<ApplicationBootstrapper>();
            services.AddSingleton<Features.AppCore.Initialization.InitializeApplicationFeature.Handler>();
            services.AddSingleton<Features.AppCore.Shutdown.CleanupApplicationFeature.Handler>();

            services.AddSingleton<ISessionState, SessionState>();

            services.AddTransient<ZhipuCompatHandler>();
            services.AddHttpClient("AiServiceClient")
                .AddHttpMessageHandler<ZhipuCompatHandler>()
                .SetHandlerLifetime(TimeSpan.FromMinutes(5));
            
            services.AddHttpClient("NativeAiClient")
                .SetHandlerLifetime(TimeSpan.FromMinutes(5));

            services.AddSingleton<SettingsService>();
            services.AddSingleton<AppConfig>(sp => sp.GetRequiredService<SettingsService>().Config);

            services.AddSingleton<WindowRegistry>();

            services.AddSingleton<OpenAiServiceFactory>();

            services.AddSingleton<FileDataService>();
            services.AddSingleton<WebDavDataService>();
            services.AddKeyedSingleton<IDataService>("cloud", (sp, key) => sp.GetRequiredService<WebDavDataService>());
            services.AddKeyedSingleton<IDataService>("local", (sp, key) => sp.GetRequiredService<FileDataService>());
            services.AddSingleton<IDataService>(sp => sp.GetRequiredService<WebDavDataService>());

            services.AddSingleton<GlobalKeyService>();
            services.AddSingleton<HotkeyService>();
            services.AddSingleton<GlobalShortcutCoordinator>();

            services.AddSingleton<IFeatureDialogProvider, FeatureDialogProvider>();
            services.AddSingleton<DialogService>();
            services.AddSingleton<WindowManager>();

            services.AddSingleton<ClipboardService>();

            services.AddSingleton<TrayService>();

            services.AddSingleton<IWindowRegistrar, LauncherWindowRegistrar>();
            services.AddSingleton<IWindowRegistrar, SettingsWindowRegistrar>();
            services.AddSingleton<IWindowRegistrar, ScreenCaptureOverlayRegistrar>();
            services.AddSingleton<IWindowRegistrar, TranslationPopupRegistrar>();
            services.AddSingleton<IWindowRegistrar, PinToScreenRegistrar>();
        }
    }
}

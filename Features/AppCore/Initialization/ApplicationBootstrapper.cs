using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using PromptMasterv6.Infrastructure.Services;
using PromptMasterv6.Infrastructure.WindowRegistration;
using PromptMasterv6.Features.Main;
using PromptMasterv6.Features.Launcher;
using PromptMasterv6.Features.Settings;
using PromptMasterv6.Features.ExternalTools;
using PromptMasterv6.Features.Main.Backup;

namespace PromptMasterv6.Features.AppCore.Initialization;

public class ApplicationBootstrapper
{
    private readonly IServiceProvider _serviceProvider;
    private readonly LoggerService _logger;

    public ApplicationBootstrapper(
        IServiceProvider serviceProvider,
        LoggerService logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task<BootstrapResult> InitializeAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInfo("Starting application initialization...", "ApplicationBootstrapper");

            await ConfigureTextBoxContextMenuAsync(cancellationToken);

            RegisterWindows();

            var mainWindow = CreateMainWindow();
            var launchBarWindow = CreateLaunchBarWindow();

            StartGlobalShortcutCoordinator();

            InitializeViewModels();

            _logger.LogInfo("Application initialized successfully.", "ApplicationBootstrapper");

            return new BootstrapResult(true, "应用程序初始化成功", mainWindow, launchBarWindow);
        }
        catch (Exception ex)
        {
            _logger.LogException(ex, "Failed to initialize application", "ApplicationBootstrapper");
            return new BootstrapResult(false, $"应用程序初始化失败: {ex.Message}", null, null);
        }
    }

    private async Task ConfigureTextBoxContextMenuAsync(CancellationToken cancellationToken)
    {
        _logger.LogInfo("Configuring TextBox context menu...", "ApplicationBootstrapper");
        
        var handler = _serviceProvider.GetService(
            typeof(Features.AppCore.UI.ConfigureTextBoxContextMenuFeature.Handler))
            as Features.AppCore.UI.ConfigureTextBoxContextMenuFeature.Handler;
        
        if (handler != null)
        {
            await handler.Handle(
                new Features.AppCore.UI.ConfigureTextBoxContextMenuFeature.Command(),
                cancellationToken);
        }
    }

    private void RegisterWindows()
    {
        _logger.LogInfo("Registering windows...", "ApplicationBootstrapper");
        
        var windowRegistry = _serviceProvider.GetRequiredService<WindowRegistry>();
        var registrars = _serviceProvider.GetServices<IWindowRegistrar>();
        
        foreach (var registrar in registrars)
        {
            registrar.Register(windowRegistry);
        }
    }

    private MainWindow CreateMainWindow()
    {
        _logger.LogInfo("Creating main window...", "ApplicationBootstrapper");
        var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        mainWindow.Show();
        return mainWindow;
    }

    private LaunchBarWindow CreateLaunchBarWindow()
    {
        _logger.LogInfo("Creating launch bar window...", "ApplicationBootstrapper");
        var launchBarWindow = _serviceProvider.GetRequiredService<LaunchBarWindow>();
        launchBarWindow.Show();
        return launchBarWindow;
    }

    private void StartGlobalShortcutCoordinator()
    {
        _logger.LogInfo("Starting global shortcut coordinator...", "ApplicationBootstrapper");
        var shortcutCoordinator = _serviceProvider.GetRequiredService<GlobalShortcutCoordinator>();
        shortcutCoordinator.Start();
    }

    private void InitializeViewModels()
    {
        _logger.LogInfo("Initializing view models...", "ApplicationBootstrapper");
        
        _ = _serviceProvider.GetRequiredService<ExternalToolsViewModel>();
        _ = _serviceProvider.GetRequiredService<BackupViewModel>();
        
        var mainViewModel = _serviceProvider.GetRequiredService<MainViewModel>();
        mainViewModel.Initialize();
    }
}

public record BootstrapResult(
    bool Success,
    string Message,
    MainWindow? MainWindow,
    LaunchBarWindow? LaunchBarWindow
);

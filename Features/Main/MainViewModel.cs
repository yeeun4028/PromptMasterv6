using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using MediatR;
using PromptMasterv6.Infrastructure.Services;
using PromptMasterv6.Features.Main.ManageFiles.Messages;
using PromptMasterv6.Features.Launcher.Messages;
using PromptMasterv6.Core.Messages;
using PromptMasterv6.Features.Main.Backup.Messages;
using PromptMasterv6.Features.Main.Sidebar.Messages;
using PromptMasterv6.Features.Main.WindowManagement;
using PromptMasterv6.Features.Main.Hotkeys;
using PromptMasterv6.Features.Main.Mode;

using System;
using System.Runtime.Versioning;
using System.Threading.Tasks;

namespace PromptMasterv6.Features.Main;

public partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly GlobalKeyService _keyService;
    private readonly SettingsService _settingsService;
    private readonly LoggerService _logger;
    private readonly IMediator _mediator;
    private readonly DialogService _dialogService;

    private EventHandler? _onLauncherTriggeredHandler;

    [ObservableProperty] private AppConfig config;
    [ObservableProperty] private bool isFullMode = true;

    public MainViewModel(
        SettingsService settingsService,
        GlobalKeyService keyService,
        LoggerService logger,
        IMediator mediator,
        DialogService dialogService)
    {
        _settingsService = settingsService;
        _keyService = keyService;
        _logger = logger;
        _mediator = mediator;
        _dialogService = dialogService;

        Config = settingsService.Config;

        RegisterMessageHandlers();
        InitializeGlobalKeyService();
        UpdateWindowHotkeys();
    }

    public void Initialize()
    {
        _ = TriggerInitializationAsync();
    }

    private void RegisterMessageHandlers()
    {
        WeakReferenceMessenger.Default.Register<ToggleWindowMessage>(this, (_, _) =>
            WeakReferenceMessenger.Default.Send(new ToggleMainWindowMessage()));

        WeakReferenceMessenger.Default.Register<TriggerLauncherMessage>(this, async (_, _) =>
            await ShowLauncherAsync());

        WeakReferenceMessenger.Default.Register<JumpToEditPromptMessage>(this, (_, m) =>
        {
            if (m.File != null)
            {
                WeakReferenceMessenger.Default.Send(new RequestSelectFileMessage(m.File, true));
            }
        });

        WeakReferenceMessenger.Default.Register<OpenSettingsRequestMessage>(this, async (_, _) =>
            await OpenSettingsAsync());

        WeakReferenceMessenger.Default.Register<Backup.Messages.BackupCompletedMessage>(this, (_, m) =>
        {
            if (m.Success)
            {
                _dialogService.ShowToast("云端备份已完成", "Success");
            }
            else
            {
                _dialogService.ShowToast($"云端备份失败: {m.ErrorMessage}", "Error");
            }
        });
    }

    private void InitializeGlobalKeyService()
    {
        _onLauncherTriggeredHandler = (_, _) => _ = ShowLauncherAsync();
        _keyService.OnLauncherTriggered += _onLauncherTriggeredHandler;

        var result = _mediator.Send(new InitializeGlobalHotkeyFeature.Command(Config.LauncherHotkey)).Result;
        if (!result.Success)
        {
            _logger.LogError(result.ErrorMessage ?? "Failed to initialize global hotkey", "MainViewModel.InitializeGlobalKeyService");
        }
    }

    private async Task TriggerInitializationAsync()
    {
        WeakReferenceMessenger.Default.Send(new ApplicationInitializedMessage());
    }

    [RelayCommand]
    private async Task EnterFullMode()
    {
        var result = await _mediator.Send(new EnterFullModeFeature.Command());
        if (result.Success)
        {
            IsFullMode = result.IsFullMode;
        }
    }

    public void OnWindowHotkeyPressed()
    {
        WeakReferenceMessenger.Default.Send(new ToggleMainWindowMessage());
    }

    public async void SimulateFullWindowHotkey()
    {
        await _mediator.Send(new SimulateHotkeyFeature.Command(Config.FullWindowHotkey));
    }

    public void ToggleModeViaHotkey()
    {
        _ = EnterFullMode();
    }

    private async Task OpenSettingsAsync()
    {
        await _mediator.Send(new OpenSettingsFeature.Command());
    }

    [RelayCommand]
    private async Task ManualBackup()
    {
        WeakReferenceMessenger.Default.Send(new RequestCloudBackupMessage());
    }

    [SupportedOSPlatform("windows")]
    public async void UpdateWindowHotkeys()
    {
        await _mediator.Send(new UpdateWindowHotkeysFeature.Command(Config.LaunchBarHotkey));
    }

    public async Task PerformLocalBackup()
    {
        WeakReferenceMessenger.Default.Send(new RequestLocalBackupMessage());
    }

    private async Task ShowLauncherAsync()
    {
        await _mediator.Send(new ShowLauncherFeature.Command());
    }

    private bool _disposed = false;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_keyService != null)
        {
            if (_onLauncherTriggeredHandler != null)
                _keyService.OnLauncherTriggered -= _onLauncherTriggeredHandler;
            _keyService.Dispose();
        }

        WeakReferenceMessenger.Default.UnregisterAll(this);

        GC.SuppressFinalize(this);
    }
}

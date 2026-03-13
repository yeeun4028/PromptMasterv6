using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using MediatR;
using PromptMasterv6.Infrastructure.Services;
using PromptMasterv6.Features.Main.FileManager;
using PromptMasterv6.Features.Main.FileManager.Messages;
using PromptMasterv6.Features.Launcher.Messages;
using PromptMasterv6.Core.Messages;
using PromptMasterv6.Features.Workspace;
using PromptMasterv6.Features.Main.ContentEditor;
using PromptMasterv6.Features.Main.ContentEditor.Messages;
using PromptMasterv6.Features.Main.Backup;
using PromptMasterv6.Features.Main.Sidebar;
using PromptMasterv6.Features.Main.Sidebar.Messages;
using PromptMasterv6.Features.Main.WindowManagement;
using PromptMasterv6.Features.Main.Hotkeys;
using PromptMasterv6.Features.Main.Mode;

using System;
using System.ComponentModel;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace PromptMasterv6.Features.Main;

public partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly GlobalKeyService _keyService;
    private readonly WindowManager _windowManager;
    private readonly SettingsService _settingsService;
    private readonly HotkeyService _hotkeyService;
    private readonly LoggerService _logger;
    private readonly IMediator _mediator;

    private readonly Subject<System.Reactive.Unit> _saveSubject = new();
    private readonly Subject<System.Reactive.Unit> _saveLocalSettingsSubject = new();

    private EventHandler? _onLauncherTriggeredHandler;
    private PropertyChangedEventHandler? _localConfigPropertyChangedHandler;

    [ObservableProperty] private AppConfig config;
    [ObservableProperty] private LocalSettings localConfig;
    [ObservableProperty] private bool isFullMode = true;

    public SettingsService SettingsService => _settingsService;

    [ObservableProperty] private FileManagerViewModel fileManagerVM;
    [ObservableProperty] private ContentEditorViewModel contentEditorVM;
    [ObservableProperty] private BackupViewModel backupVM;
    [ObservableProperty] private SidebarViewModel sidebarVM;

    public MainViewModel(
        SettingsService settingsService,
        GlobalKeyService keyService,
        WindowManager windowManager,
        HotkeyService hotkeyService,
        LoggerService logger,
        FileManagerViewModel fileManagerVM,
        ContentEditorViewModel contentEditorVM,
        BackupViewModel backupVM,
        SidebarViewModel sidebarVM,
        IMediator mediator)
    {
        _settingsService = settingsService;
        _keyService = keyService;
        _windowManager = windowManager;
        _hotkeyService = hotkeyService;
        _logger = logger;
        _mediator = mediator;

        FileManagerVM = fileManagerVM;
        ContentEditorVM = contentEditorVM;
        BackupVM = backupVM;
        SidebarVM = sidebarVM;

        Config = settingsService.Config;
        LocalConfig = settingsService.LocalConfig;

        RegisterMessageHandlers();
        InitializeReactiveStreams();
        InitializeGlobalKeyService();
        UpdateWindowHotkeys();

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
                FileManagerVM.SelectedFile = m.File;
                WeakReferenceMessenger.Default.Send(new RequestSelectFileMessage(m.File, true));
            }
        });

        WeakReferenceMessenger.Default.Register<OpenSettingsRequestMessage>(this, async (_, _) =>
            await OpenSettingsAsync());

        WeakReferenceMessenger.Default.Register<Backup.Messages.BackupCompletedMessage>(this, (_, m) =>
        {
            if (m.Success)
            {
                HandyControl.Controls.Growl.Success("云端备份已完成");
            }
            else
            {
                HandyControl.Controls.Growl.Error($"云端备份失败: {m.ErrorMessage}");
            }
        });
    }

    private void InitializeReactiveStreams()
    {
        _saveSubject
            .Throttle(TimeSpan.FromSeconds(5))
            .ObserveOn(System.Threading.SynchronizationContext.Current!)
            .Subscribe(_ => WeakReferenceMessenger.Default.Send(new RequestLocalBackupMessage()));

        _saveLocalSettingsSubject
            .Throttle(TimeSpan.FromSeconds(2))
            .ObserveOn(System.Threading.SynchronizationContext.Current!)
            .Subscribe(_ => _settingsService.SaveLocalConfig());

        _localConfigPropertyChangedHandler = (_, e) =>
        {
            if (e.PropertyName == nameof(LocalSettings.Block1Width) ||
                e.PropertyName == nameof(LocalSettings.Block2Width))
            {
                _saveLocalSettingsSubject.OnNext(System.Reactive.Unit.Default);
            }
        };
        LocalConfig.PropertyChanged += _localConfigPropertyChangedHandler;
    }

    private void InitializeGlobalKeyService()
    {
        _onLauncherTriggeredHandler = (_, _) => _ = ShowLauncherAsync();
        _keyService.OnLauncherTriggered += _onLauncherTriggeredHandler;

        _keyService.LauncherHotkeyString = Config.LauncherHotkey;
        try { _keyService.Start(); }
        catch (Exception ex)
        {
            _logger.LogException(ex, "Failed to start GlobalKeyService", "MainViewModel.InitializeGlobalKeyService");
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

    public void SimulateFullWindowHotkey()
    {
        _hotkeyService.SimulateHotkey(Config.FullWindowHotkey);
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

        _saveSubject?.Dispose();
        _saveLocalSettingsSubject?.Dispose();

        if (_keyService != null)
        {
            if (_onLauncherTriggeredHandler != null)
                _keyService.OnLauncherTriggered -= _onLauncherTriggeredHandler;
            _keyService.Dispose();
        }

        if (LocalConfig != null && _localConfigPropertyChangedHandler != null)
        {
            LocalConfig.PropertyChanged -= _localConfigPropertyChangedHandler;
        }

        FileManagerVM?.Cleanup();
        BackupVM?.Cleanup();
        SidebarVM?.Dispose();

        WeakReferenceMessenger.Default.UnregisterAll(this);

        GC.SuppressFinalize(this);
    }
}

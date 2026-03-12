using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
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
        SidebarViewModel sidebarVM)
    {
        _settingsService = settingsService;
        _keyService = keyService;
        _windowManager = windowManager;
        _hotkeyService = hotkeyService;
        _logger = logger;

        FileManagerVM = fileManagerVM;
        ContentEditorVM = contentEditorVM;
        BackupVM = backupVM;
        SidebarVM = sidebarVM;

        Config = settingsService.Config;
        LocalConfig = settingsService.LocalConfig;

        WeakReferenceMessenger.Default.Register<RequestSaveMessage>(this, (_, _) => FileManagerVM.RequestSaveCommand.Execute(null));
        WeakReferenceMessenger.Default.Register<RequestBackupMessage>(this, async (_, _) => await BackupVM.PerformLocalBackupCommand.ExecuteAsync(null));
        WeakReferenceMessenger.Default.Register<RequestCloudBackupMessage>(this, async (_, _) => await ManualBackup());
        WeakReferenceMessenger.Default.Register<ToggleWindowMessage>(this, (_, _) => ToggleMainWindow());
        WeakReferenceMessenger.Default.Register<TriggerLauncherMessage>(this, (_, _) => HandleLauncherTriggered());
        WeakReferenceMessenger.Default.Register<JumpToEditPromptMessage>(this, (_, m) =>
        {
            if (m.File != null)
            {
                FileManagerVM.SelectedFile = m.File;
                WeakReferenceMessenger.Default.Send(new RequestSelectFileMessage(m.File, true));
            }
        });

        WeakReferenceMessenger.Default.Register<OpenSettingsRequestMessage>(this, (_, _) => OpenSettings());
        
        WeakReferenceMessenger.Default.Register<ToggleEditModeRequestMessage>(this, async (_, _) => 
        {
            await ContentEditorVM.ToggleEditModeCommand.ExecuteAsync(null);
        });

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

        _saveSubject
            .Throttle(TimeSpan.FromSeconds(5))
            .ObserveOn(System.Threading.SynchronizationContext.Current!)
            .Subscribe(async _ => await BackupVM.PerformLocalBackupCommand.ExecuteAsync(null));

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

        _onLauncherTriggeredHandler = (_, _) => HandleLauncherTriggered();
        _keyService.OnLauncherTriggered += _onLauncherTriggeredHandler;

        _keyService.LauncherHotkeyString = Config.LauncherHotkey;
        try { _keyService.Start(); }
        catch (Exception ex)
        {
            _logger.LogException(ex, "Failed to start GlobalKeyService", "MainViewModel.ctor");
        }

        UpdateWindowHotkeys();

        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        await FileManagerVM.InitializeAsync();
    }

    [RelayCommand]
    private void EnterFullMode()
    {
        IsFullMode = true;
    }

    public void OnWindowHotkeyPressed()
    {
        ToggleMainWindow();
    }

    public void SimulateFullWindowHotkey()
    {
        _hotkeyService.SimulateHotkey(Config.FullWindowHotkey);
    }

    public void ToggleModeViaHotkey()
    {
        EnterFullMode();
    }

    [RelayCommand]
    private void OpenSettings()
    {
        _windowManager.ShowSettingsWindow();
    }

    [RelayCommand]
    private async Task ManualBackup()
    {
        await BackupVM.PerformCloudBackupCommand.ExecuteAsync(null);
    }

    private void ToggleMainWindow()
    {
        var win = System.Windows.Application.Current.MainWindow as MainWindow;
        if (win == null) return;

        win.ToggleWindowVisibility();
    }

    [SupportedOSPlatform("windows")]
    public void UpdateWindowHotkeys()
    {
        _hotkeyService.RegisterWindowHotkey("ToggleLaunchBarHotkey", Config.LaunchBarHotkey, () => 
        {
            Config.EnableLaunchBar = !Config.EnableLaunchBar;
        });
    }

    public async Task PerformLocalBackup()
    {
        await BackupVM.PerformLocalBackupCommand.ExecuteAsync(null);
    }

    public void HandleLauncherTriggered()
    {
        _windowManager.ShowLauncherWindow();
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

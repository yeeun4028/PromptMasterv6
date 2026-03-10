using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using PromptMasterv6.Infrastructure.Services;
using PromptMasterv6.Features.Main.Messages;
using PromptMasterv6.Features.Launcher.Messages;
using PromptMasterv6.Core.Messages;
using PromptMasterv6.Features.Workspace;

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

    private DispatcherTimer _timer;
    private readonly Subject<System.Reactive.Unit> _saveSubject = new();
    private readonly Subject<System.Reactive.Unit> _saveLocalSettingsSubject = new();

    private EventHandler? _onLauncherTriggeredHandler;
    private PropertyChangedEventHandler? _localConfigPropertyChangedHandler;

    private bool _isSimulatingKeys;
    public void SetSimulatingKeys(bool value) => _isSimulatingKeys = value;

    [ObservableProperty] private AppConfig config;
    [ObservableProperty] private LocalSettings localConfig;
    [ObservableProperty] private bool isFullMode = true;

    public SettingsService SettingsService => _settingsService;

    [ObservableProperty] private string syncTimeDisplay = "Now";

    [ObservableProperty] private FileManagerViewModel fileManagerVM;
    [ObservableProperty] private ContentEditorViewModel contentEditorVM;
    [ObservableProperty] private WorkspaceViewModel? workspaceVM;

    public MainViewModel(
        SettingsService settingsService,
        GlobalKeyService keyService,
        WindowManager windowManager,
        HotkeyService hotkeyService,
        LoggerService logger,
        FileManagerViewModel fileManagerVM,
        ContentEditorViewModel contentEditorVM)
    {
        _settingsService = settingsService;
        _keyService = keyService;
        _windowManager = windowManager;
        _hotkeyService = hotkeyService;
        _logger = logger;

        FileManagerVM = fileManagerVM;
        ContentEditorVM = contentEditorVM;

        Config = settingsService.Config;
        LocalConfig = settingsService.LocalConfig;

        FileManagerVM.SaveRequested += async () => await PerformLocalBackup();
        FileManagerVM.SelectedFileChanged += async file => 
        {
            await ContentEditorVM.SetCurrentFileAsync(file);
        };

        ContentEditorVM.ContentChanged += () =>
        {
            FileManagerVM.RequestSaveCommand.Execute(null);
        };

        WeakReferenceMessenger.Default.Register<RequestSaveMessage>(this, (_, _) => FileManagerVM.RequestSaveCommand.Execute(null));
        WeakReferenceMessenger.Default.Register<RequestBackupMessage>(this, async (_, _) => await PerformLocalBackup());
        WeakReferenceMessenger.Default.Register<ToggleWindowMessage>(this, (_, _) => ToggleMainWindow());
        WeakReferenceMessenger.Default.Register<TriggerLauncherMessage>(this, (_, _) => HandleLauncherTriggered());
        WeakReferenceMessenger.Default.Register<JumpToEditPromptMessage>(this, (_, m) =>
        {
            if (m.File != null)
            {
                FileManagerVM.SelectedFile = m.File;
                ContentEditorVM.IsEditMode = true;
            }
        });

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) => UpdateTimeDisplay();
        _timer.Start();

        _saveSubject
            .Throttle(TimeSpan.FromSeconds(5))
            .ObserveOn(System.Threading.SynchronizationContext.Current!)
            .Subscribe(async _ => await PerformLocalBackup());

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
        try
        {
            await PerformLocalBackup();

            await FileManagerVM.PerformCloudBackupAsync();

            LocalConfig.LastCloudSyncTime = DateTime.Now;
            _settingsService.SaveLocalConfig();

            UpdateTimeDisplay();

            HandyControl.Controls.Growl.Success("云端备份已完成");
        }
        catch (Exception ex)
        {
            HandyControl.Controls.Growl.Error($"云端备份失败: {ex.Message}");
        }
    }

    [RelayCommand]
    private void ChangeActionIcon(string actionKey)
    {
        if (string.IsNullOrWhiteSpace(actionKey)) return;

        var currentIcon = LocalConfig.ActionIcons != null && LocalConfig.ActionIcons.TryGetValue(actionKey, out var icon) ? icon : "";

        var dialog = new IconInputDialog(currentIcon);
        if (dialog.ShowDialog() == true)
        {
            if (LocalConfig.ActionIcons == null)
            {
                LocalConfig.ActionIcons = new System.Collections.Generic.Dictionary<string, string>();
            }

            LocalConfig.ActionIcons[actionKey] = dialog.ResultGeometry;
            
            _settingsService.SaveLocalConfig();

            OnPropertyChanged(nameof(LocalConfig));
        }
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

    private void ToggleWindowToMode(bool targetFull)
    {
        var win = System.Windows.Application.Current.MainWindow;
        if (win == null) return;

        if (win.Visibility != Visibility.Visible)
        {
            IsFullMode = true;
            win.Show();
            win.Activate();
            return;
        }

        win.Hide();
    }

    public async Task PerformLocalBackup()
    {
        await FileManagerVM.PerformLocalBackupAsync();
    }

    private void UpdateTimeDisplay()
    {
        if (LocalConfig?.LastCloudSyncTime == null)
        {
            SyncTimeDisplay = "-";
            return;
        }

        var diff = DateTime.Now - LocalConfig.LastCloudSyncTime.Value;

        if (diff.TotalSeconds < 60)
        {
            SyncTimeDisplay = $"{(int)diff.TotalSeconds}s";
        }
        else if (diff.TotalMinutes < 60)
        {
            SyncTimeDisplay = $"{(int)diff.TotalMinutes}min";
        }
        else if (diff.TotalHours < 24)
        {
            SyncTimeDisplay = $"{(int)diff.TotalHours}H";
        }
        else
        {
            SyncTimeDisplay = $"{(int)diff.TotalDays}d";
        }
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

        if (_timer != null)
        {
            _timer.Stop();
            _timer = null!;
        }

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

        WeakReferenceMessenger.Default.UnregisterAll(this);

        GC.SuppressFinalize(this);
    }
}

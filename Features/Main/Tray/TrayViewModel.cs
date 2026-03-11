using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using PromptMasterv6.Core.Messages;
using PromptMasterv6.Infrastructure.Services;

namespace PromptMasterv6.Features.Main.Tray;

public partial class TrayViewModel : ObservableObject
{
    private readonly WindowManager _windowManager;
    private readonly LoggerService _logger;
    private readonly SettingsService _settingsService;

    public event Action? ToggleWindowRequested;
    public event Func<Task>? ExitRequested;
    public Func<bool>? IsDirtyCheck { get; set; }

    public TrayViewModel(
        WindowManager windowManager,
        LoggerService logger,
        SettingsService settingsService)
    {
        _windowManager = windowManager;
        _logger = logger;
        _settingsService = settingsService;
    }

    [RelayCommand]
    private void ToggleVisibility()
    {
        ToggleWindowRequested?.Invoke();
    }

    [RelayCommand]
    private async Task ManualBackup()
    {
        WeakReferenceMessenger.Default.Send(new RequestBackupMessage());
    }

    [RelayCommand]
    private void OpenSettings()
    {
        _windowManager.ShowSettingsWindow();
    }

    [RelayCommand]
    private async Task Exit()
    {
        if (ExitRequested != null)
        {
            await ExitRequested.Invoke();
        }
    }

    [RelayCommand]
    private async Task PinToScreenFromCapture()
    {
        try
        {
            await _windowManager.ShowPinToScreenFromCaptureAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError($"贴图截图失败: {ex.Message}", "TrayViewModel");
        }
    }

    [RelayCommand]
    private void PinToScreenFromClipboard()
    {
        try
        {
            if (!_windowManager.ShowPinToScreenFromClipboard())
            {
                HandyControl.Controls.Growl.Warning("剪贴板中没有图片");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"贴图剪贴板失败: {ex.Message}", "TrayViewModel");
        }
    }

    [RelayCommand]
    private void CloseAllPinToScreen()
    {
        try
        {
            _windowManager.CloseAllPinToScreenWindows();
        }
        catch (Exception ex)
        {
            _logger.LogError($"关闭贴图失败: {ex.Message}", "TrayViewModel");
        }
    }
}

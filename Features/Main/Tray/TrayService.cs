using System;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.Messaging;
using PromptMasterv6.Core.Messages;
using PromptMasterv6.Infrastructure.Helpers;
using PromptMasterv6.Infrastructure.Services;

namespace PromptMasterv6.Features.Main.Tray;

public class TrayService : IDisposable
{
    private readonly LoggerService _logger;
    private readonly WindowManager _windowManager;
    private System.Windows.Forms.NotifyIcon? _notifyIcon;
    private DispatcherTimer? _routingAnimTimer;
    private bool _routingAnimState;
    private System.Drawing.Icon? _defaultIcon;
    private System.Drawing.Icon? _processingIcon;

    private Action? _onExitRequested;
    private Func<bool>? _onIsDirtyCheck;
    private Action? _onToggleWindow;

    public TrayService(LoggerService logger, WindowManager windowManager)
    {
        _logger = logger;
        _windowManager = windowManager;

        WeakReferenceMessenger.Default.Register<ToggleMainWindowMessage>(this, (_, _) =>
        {
            _onToggleWindow?.Invoke();
        });
    }

    public void Initialize(
        object dataContext,
        Action onToggleWindow,
        Action onExitRequested,
        Func<bool> onIsDirtyCheck)
    {
        _onToggleWindow = onToggleWindow;
        _onExitRequested = onExitRequested;
        _onIsDirtyCheck = onIsDirtyCheck;

        InitializeNotifyIcon(dataContext);
    }

    private void InitializeNotifyIcon(object dataContext)
    {
        try
        {
            _notifyIcon = new System.Windows.Forms.NotifyIcon();

            try
            {
                _notifyIcon.Icon = System.Drawing.Icon.ExtractAssociatedIcon(System.Windows.Forms.Application.ExecutablePath);
            }
            catch
            {
                try
                {
                    if (System.IO.File.Exists("pro_icon.ico"))
                        _notifyIcon.Icon = new System.Drawing.Icon("pro_icon.ico");
                    else
                        _notifyIcon.Icon = System.Drawing.SystemIcons.Application;
                }
                catch
                {
                    _notifyIcon.Icon = System.Drawing.SystemIcons.Application;
                }
            }

            _notifyIcon.Text = "PromptMaster v6";
            _notifyIcon.Visible = true;

            _notifyIcon.MouseClick += (s, e) =>
            {
                if (e.Button == System.Windows.Forms.MouseButtons.Right)
                {
                    ShowContextMenu(dataContext);
                }
                else if (e.Button == System.Windows.Forms.MouseButtons.Left)
                {
                    _onToggleWindow?.Invoke();
                }
            };

            _defaultIcon = _notifyIcon.Icon;
            _processingIcon = System.Drawing.SystemIcons.Information;

            _routingAnimTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            _routingAnimTimer.Tick += (s, e) =>
            {
                if (_notifyIcon == null) return;
                _routingAnimState = !_routingAnimState;
                _notifyIcon.Icon = _routingAnimState ? _processingIcon : _defaultIcon;
            };
        }
        catch (Exception ex)
        {
            _logger.LogError($"托盘图标初始化失败: {ex.Message}", "TrayService.InitializeNotifyIcon");
        }
    }

    private void ShowContextMenu(object dataContext)
    {
        var menu = System.Windows.Application.Current.MainWindow?.Resources["TrayMenu"] as System.Windows.Controls.ContextMenu;
        if (menu != null)
        {
            menu.DataContext = dataContext;
            TrayMenuHelper.ShowContextMenu(menu);
        }
    }

    public void Exit()
    {
        _onExitRequested?.Invoke();
    }

    public void Dispose()
    {
        if (_notifyIcon != null)
        {
            try
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
                _logger.LogInfo("[TrayService] NotifyIcon disposed", "TrayService.Dispose");
            }
            catch (Exception ex)
            {
                _logger.LogError($"[TrayService] Dispose error: {ex.Message}", "TrayService.Dispose");
            }
            _notifyIcon = null;
        }

        _routingAnimTimer?.Stop();
        _routingAnimTimer = null;

        WeakReferenceMessenger.Default.UnregisterAll(this);
    }
}

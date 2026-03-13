using System;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.Messaging;
using PromptMasterv6.Core.Messages;
using PromptMasterv6.Infrastructure.Helpers;
using PromptMasterv6.Features.Main.Tray;

namespace PromptMasterv6.Infrastructure.Services;

public class TrayService : IDisposable
{
    private readonly LoggerService _logger;
    private readonly TrayViewModel _viewModel;
    private System.Windows.Forms.NotifyIcon? _notifyIcon;
    private DispatcherTimer? _routingAnimTimer;
    private bool _routingAnimState;
    private System.Drawing.Icon? _defaultIcon;
    private System.Drawing.Icon? _processingIcon;

    private TrayMenuView? _trayMenu;

    public TrayService(LoggerService logger, TrayViewModel viewModel)
    {
        _logger = logger;
        _viewModel = viewModel;
    }

    public void Initialize(
        Action onToggleWindow,
        Func<Task> onExitRequested,
        Func<bool>? onIsDirtyCheck = null)
    {
        _viewModel.ToggleWindowRequested += onToggleWindow;
        _viewModel.ExitRequested += onExitRequested;
        _viewModel.IsDirtyCheck = onIsDirtyCheck;

        InitializeNotifyIcon();
    }

    private void InitializeNotifyIcon()
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
                    ShowContextMenu();
                }
                else if (e.Button == System.Windows.Forms.MouseButtons.Left)
                {
                    _viewModel.ToggleVisibilityCommand.Execute(null);
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

    private void ShowContextMenu()
    {
        if (_trayMenu == null)
        {
            _trayMenu = new TrayMenuView();
        }
        
        _trayMenu.DataContext = _viewModel;
        TrayMenuHelper.ShowContextMenu(_trayMenu);
    }

    public void Exit()
    {
        _viewModel.ExitCommand.Execute(null);
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

        _trayMenu = null;

        WeakReferenceMessenger.Default.UnregisterAll(this);
    }
}

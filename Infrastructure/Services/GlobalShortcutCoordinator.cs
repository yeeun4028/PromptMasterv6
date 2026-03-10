using PromptMasterv6.Core.Messages;
using CommunityToolkit.Mvvm.Messaging;
using System;

namespace PromptMasterv6.Infrastructure.Services
{
    public class GlobalShortcutCoordinator
    {
        private readonly SettingsService _settingsService;
        private readonly HotkeyService _hotkeyService;
        private readonly GlobalKeyService _globalKeyService;
        private readonly WindowManager _windowManager;

        public GlobalShortcutCoordinator(
            SettingsService settingsService,
            HotkeyService hotkeyService,
            GlobalKeyService globalKeyService,
            WindowManager windowManager)
        {
            _settingsService = settingsService;
            _hotkeyService = hotkeyService;
            _globalKeyService = globalKeyService;
            _windowManager = windowManager;

            WeakReferenceMessenger.Default.Register<ReloadDataMessage>(this, (_, __) =>
            {
                System.Windows.Application.Current.Dispatcher.BeginInvoke(RegisterAllHotkeys);
            });

            _globalKeyService.OnLauncherTriggered += (s, e) =>
            {
                System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    _windowManager.ShowLauncherWindow();
                }));
            };
        }

        public void Start()
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                _globalKeyService.Start();
                RegisterAllHotkeys();
            });
        }

        private void RegisterAllHotkeys()
        {
            var config = _settingsService.Config;
            var failedHotkeys = new System.Collections.Generic.List<string>();

            if (string.IsNullOrWhiteSpace(config.LauncherHotkey))
            {
                failedHotkeys.Add("全局启动器 (未配置快捷键或配置为空)");
            }
            else
            {
                try
                {
                    _globalKeyService.LauncherHotkeyString = config.LauncherHotkey;
                }
                catch (FormatException)
                {
                    failedHotkeys.Add($"全局启动器 (格式非法): {config.LauncherHotkey}");
                }
                catch (Exception)
                {
                    failedHotkeys.Add($"全局启动器 (注册失败): {config.LauncherHotkey}");
                }
            }

            if (!_hotkeyService.RegisterWindowHotkey("OcrHotkey", config.OcrHotkey, () =>
            {
                System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    WeakReferenceMessenger.Default.Send(new TriggerOcrMessage());
                }));
            }))
            {
                failedHotkeys.Add($"OCR截图: {config.OcrHotkey}");
            }

            if (!_hotkeyService.RegisterWindowHotkey("TranslateHotkey", config.ScreenshotTranslateHotkey, () =>
            {
                System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    WeakReferenceMessenger.Default.Send(new TriggerTranslateMessage());
                }));
            }))
            {
                failedHotkeys.Add($"截图翻译: {config.ScreenshotTranslateHotkey}");
            }

            if (!_hotkeyService.RegisterWindowHotkey("PinToScreenHotkey", config.PinToScreenHotkey, () =>
            {
                System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    WeakReferenceMessenger.Default.Send(new TriggerPinToScreenMessage());
                }));
            }))
            {
                failedHotkeys.Add($"贴图: {config.PinToScreenHotkey}");
            }

            if (!_hotkeyService.RegisterWindowHotkey("FullWindowHotkey", config.FullWindowHotkey, () =>
            {
                System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    WeakReferenceMessenger.Default.Send(new ToggleMainWindowMessage());
                }));
            }))
            {
                failedHotkeys.Add($"主界面切换: {config.FullWindowHotkey}");
            }

            if (failedHotkeys.Count > 0)
            {
                System.Windows.Application.Current.Dispatcher.BeginInvoke(
                    new Action(() =>
                    {
                        System.Windows.MessageBox.Show(
                            $"以下全局快捷键已被其他程序占用，注册失败：\n\n" +
                            $"{string.Join("\n", failedHotkeys)}\n\n" +
                            $"请前往设置界面修改为您专属的无冲突按键组合。",
                            "快捷键冲突警告",
                            System.Windows.MessageBoxButton.OK,
                            System.Windows.MessageBoxImage.Warning);
                    }),
                    System.Windows.Threading.DispatcherPriority.Loaded);
            }
        }
    }
}

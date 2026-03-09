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
                System.Windows.Application.Current.Dispatcher.Invoke(RegisterAllHotkeys);
            });

            _globalKeyService.OnLauncherTriggered += (s, e) =>
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    _windowManager.ShowLauncherWindow();
                });
            };
        }

        public void Start()
        {
            _globalKeyService.Start();
            RegisterAllHotkeys();
        }

        private void RegisterAllHotkeys()
        {
            var config = _settingsService.Config;

            _globalKeyService.LauncherHotkeyString = config.LauncherHotkey;

            _hotkeyService.RegisterWindowHotkey("OcrHotkey", config.OcrHotkey, () =>
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    WeakReferenceMessenger.Default.Send(new TriggerOcrMessage());
                });
            });

            _hotkeyService.RegisterWindowHotkey("TranslateHotkey", config.ScreenshotTranslateHotkey, () =>
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    WeakReferenceMessenger.Default.Send(new TriggerTranslateMessage());
                });
            });

            _hotkeyService.RegisterWindowHotkey("PinToScreenHotkey", config.PinToScreenHotkey, () =>
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    WeakReferenceMessenger.Default.Send(new TriggerPinToScreenMessage());
                });
            });

            _hotkeyService.RegisterWindowHotkey("FullWindowHotkey", config.FullWindowHotkey, () =>
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    WeakReferenceMessenger.Default.Send(new ToggleMainWindowMessage());
                });
            });
        }
    }
}

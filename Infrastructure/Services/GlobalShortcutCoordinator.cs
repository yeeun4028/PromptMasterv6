using Microsoft.Extensions.DependencyInjection;
using PromptMasterv6.Core.Interfaces;
using PromptMasterv6.Features.Shared.Messages;
using PromptMasterv6.Features.ExternalTools;
using CommunityToolkit.Mvvm.Messaging;
using System;

namespace PromptMasterv6.Infrastructure.Services
{
    public class GlobalShortcutCoordinator
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ISettingsService _settingsService;
        private readonly HotkeyService _hotkeyService;
        private readonly GlobalKeyService _globalKeyService;
        private readonly IWindowManager _windowManager;

        public GlobalShortcutCoordinator(
            IServiceProvider serviceProvider,
            ISettingsService settingsService,
            HotkeyService hotkeyService,
            GlobalKeyService globalKeyService,
            IWindowManager windowManager)
        {
            _serviceProvider = serviceProvider;
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
                    var externalVM = _serviceProvider.GetRequiredService<ExternalToolsViewModel>();
                    externalVM.TriggerOcrCommand.Execute(null);
                });
            });

            _hotkeyService.RegisterWindowHotkey("TranslateHotkey", config.ScreenshotTranslateHotkey, () =>
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    var externalVM = _serviceProvider.GetRequiredService<ExternalToolsViewModel>();
                    externalVM.TriggerTranslateCommand.Execute(null);
                });
            });

            _hotkeyService.RegisterWindowHotkey("PinToScreenHotkey", config.PinToScreenHotkey, () =>
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    var externalVM = _serviceProvider.GetRequiredService<ExternalToolsViewModel>();
                    externalVM.TriggerPinToScreenCommand.Execute(null);
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

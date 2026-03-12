using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using MediatR;
using PromptMasterv6.Infrastructure.Services;
using PromptMasterv6.Core.Messages;
using PromptMasterv6.Features.ExternalTools.PerformScreenshotOcr;
using PromptMasterv6.Features.ExternalTools.PerformScreenshotTranslate;
using PromptMasterv6.Features.ExternalTools.EnsureAiProfile;
using PromptMasterv6.Features.Main.Tray;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace PromptMasterv6.Features.ExternalTools
{
    public partial class ExternalToolsViewModel : ObservableObject
    {
        private bool _isCapturing;
        private readonly SettingsService _settingsService;
        private readonly IMediator _mediator;
        private readonly DialogService _dialogService;
        private readonly WindowManager _windowManager;
        private readonly LoggerService _logger;

        public ObservableCollection<ApiProfile> OcrProfiles =>
            new(Config.ApiProfiles.Where(p => p.ServiceType == ServiceType.OCR));

        public ObservableCollection<ApiProfile> TranslateProfiles =>
            new(Config.ApiProfiles.Where(p => p.ServiceType == ServiceType.Translation));

        public void RefreshProfiles()
        {
            OnPropertyChanged(nameof(OcrProfiles));
            OnPropertyChanged(nameof(TranslateProfiles));
        }

        public AppConfig Config => _settingsService.Config;
        public LocalSettings LocalConfig => _settingsService.LocalConfig;

        public ExternalToolsViewModel(
            SettingsService settingsService,
            IMediator mediator,
            DialogService dialogService,
            WindowManager windowManager,
            LoggerService logger)
        {
            _settingsService = settingsService;
            _mediator = mediator;
            _dialogService = dialogService;
            _windowManager = windowManager;
            _logger = logger;

            WeakReferenceMessenger.Default.Register<TriggerOcrMessage>(this, async (_, _) => await TriggerOcr());
            WeakReferenceMessenger.Default.Register<TriggerTranslateMessage>(this, async (_, _) => await TriggerTranslate());
            WeakReferenceMessenger.Default.Register<TriggerPinToScreenMessage>(this, async (_, _) => await TriggerPinToScreen());

            _logger.LogInfo("ExternalToolsViewModel initialized", "ExternalToolsViewModel.ctor");

            _ = EnsureAiProfileAsync();
        }

        private async Task EnsureAiProfileAsync()
        {
            await _mediator.Send(new EnsureAiProfileFeature.Command());
            RefreshProfiles();
        }

        [RelayCommand]
        private async Task TriggerOcr()
        {
            if (_isCapturing) return;

            try
            {
                _isCapturing = true;

                var result = await _mediator.Send(new PerformScreenshotOcrFeature.Command());

                if (result.ShouldOpenSettings)
                {
                    if (_dialogService.ShowOcrNotConfiguredDialog())
                    {
                        WeakReferenceMessenger.Default.Send(new OpenSettingsMessage { TabIndex = 2 });
                    }
                    return;
                }

                if (!result.Success)
                {
                    if (!string.IsNullOrEmpty(result.ErrorMessage))
                    {
                        _dialogService.ShowAlert(result.ErrorMessage, "OCR 失败");
                    }
                    return;
                }

                // 成功：复制到剪贴板
                if (!string.IsNullOrWhiteSpace(result.OcrText))
                {
                    System.Windows.Clipboard.SetText(result.OcrText);
                }
            }
            finally
            {
                _isCapturing = false;
            }
        }

        [RelayCommand]
        private async Task TriggerTranslate()
        {
            if (_isCapturing) return;

            try
            {
                _isCapturing = true;

                var result = await _mediator.Send(new PerformScreenshotTranslateFeature.Command());

                if (result.ShouldOpenSettings)
                {
                    if (_dialogService.ShowOcrNotConfiguredDialog())
                    {
                        WeakReferenceMessenger.Default.Send(new OpenSettingsMessage { TabIndex = 2 });
                    }
                    return;
                }

                if (!result.Success)
                {
                    if (!string.IsNullOrEmpty(result.ErrorMessage))
                    {
                        _dialogService.ShowAlert(result.ErrorMessage, "识别/翻译失败");
                    }
                    else if (result.ShouldShowPopup && !string.IsNullOrEmpty(result.TranslatedText))
                    {
                        _windowManager.ShowTranslationPopup(result.TranslatedText);
                    }
                    return;
                }

                // 成功：自动复制并显示弹窗
                if (Config.AutoCopyTranslationResult && !string.IsNullOrWhiteSpace(result.TranslatedText))
                {
                    try { System.Windows.Clipboard.SetText(result.TranslatedText); } catch { }
                }

                _windowManager.ShowTranslationPopup(result.TranslatedText ?? "翻译失败", result.ActionRect);
            }
            finally
            {
                _isCapturing = false;
            }
        }

        [RelayCommand]
        private async Task TriggerPinToScreen()
        {
            if (_isCapturing) return;

            try
            {
                _isCapturing = true;
                await _mediator.Send(new PinToScreenFromCaptureFeature.Command());
            }
            finally
            {
                _isCapturing = false;
            }
        }
    }
}

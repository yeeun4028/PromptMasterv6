using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using PromptMasterv6.Infrastructure.Services;
using PromptMasterv6.Core.Interfaces;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Messaging;
using PromptMasterv6.Core.Messages;
using PromptMasterv6.Features.Workspace.Messages;
using PromptMasterv6.Features.AiModels.Messages;
using PromptMasterv6.Features.Settings.ExternalTools.HandleAiModelDeleted;
using PromptMasterv6.Features.Shared.Models;
using System.Collections.Specialized;
using System.Linq;
using System.Threading.Tasks;

namespace PromptMasterv6.Features.Settings.ExternalTools
{
    public partial class ExternalToolsSettingsViewModel : ObservableObject, IRecipient<AiModelDeletedMessage>
    {
        private readonly SettingsService _settingsService;
        private readonly ISessionState _sessionState;
        private readonly DialogService _dialogService;
        private readonly IMediator _mediator;

        public AppConfig Config => _settingsService.Config;
        public ObservableCollection<PromptItem> FilesView => _sessionState.Files;

        public ObservableCollection<ApiProfile> OcrProfiles { get; }
        public ObservableCollection<ApiProfile> TranslateProfiles { get; }

        [ObservableProperty] private int selectedSubTab = 0;
        [ObservableProperty] private string? translationTestStatus;
        [ObservableProperty] private System.Windows.Media.Brush translationTestStatusColor = System.Windows.Media.Brushes.Gray;

        [RelayCommand]
        private async Task SelectSubTab(string tabIndexStr)
        {
            var result = await _mediator.Send(new SelectSubTabFeature.Command(tabIndexStr));
            if (result.Success)
            {
                SelectedSubTab = result.SelectedTabIndex;
            }
        }

        public ExternalToolsSettingsViewModel(
            SettingsService settingsService,
            ISessionState sessionState,
            DialogService dialogService,
            IMediator mediator)
        {
            _settingsService = settingsService;
            _sessionState = sessionState;
            _dialogService = dialogService;
            _mediator = mediator;

            OcrProfiles = new ObservableCollection<ApiProfile>(
                _settingsService.Config.ApiProfiles.Where(p => p.ServiceType == ServiceType.OCR));
            TranslateProfiles = new ObservableCollection<ApiProfile>(
                _settingsService.Config.ApiProfiles.Where(p => p.ServiceType == ServiceType.Translation));

            _settingsService.Config.ApiProfiles.CollectionChanged += OnApiProfilesChanged;

            WeakReferenceMessenger.Default.Register(this);
        }

        private void OnApiProfilesChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            RefreshProfiles();
        }

        public void RefreshProfiles()
        {
            OcrProfiles.Clear();
            TranslateProfiles.Clear();

            foreach (var profile in _settingsService.Config.ApiProfiles)
            {
                if (profile.ServiceType == ServiceType.OCR)
                    OcrProfiles.Add(profile);
                else if (profile.ServiceType == ServiceType.Translation)
                    TranslateProfiles.Add(profile);
            }

            OnPropertyChanged(nameof(OcrProfiles));
            OnPropertyChanged(nameof(TranslateProfiles));
        }

        [RelayCommand]
        private void JumpToEditPrompt()
        {
            var promptId = Config.AiTranslationPromptId;
            if (string.IsNullOrWhiteSpace(promptId)) return;

            var msg = new RequestPromptFileMessage { PromptId = promptId };
            WeakReferenceMessenger.Default.Send(msg);
            if (msg.HasReceivedResponse && msg.Response != null && msg.Response.File != null)
            {
                WeakReferenceMessenger.Default.Send(new JumpToEditPromptMessage { File = msg.Response.File });
            }
        }

        [RelayCommand]
        private async Task SaveAiTranslationConfig()
        {
            var promptId = Config.AiTranslationPromptId;
            var promptTitle = "";
            if (!string.IsNullOrWhiteSpace(promptId))
            {
                var msg = new RequestPromptFileMessage { PromptId = promptId };
                WeakReferenceMessenger.Default.Send(msg);
                promptTitle = msg.HasReceivedResponse ? msg.Response?.File?.Title ?? "" : "";
            }

            var result = await _mediator.Send(new SaveAiTranslationConfigFeature.Command(
                promptId,
                promptTitle,
                Config.AiBaseUrl,
                Config.AiApiKey,
                Config.AiModel
            ));

            if (result.Success)
            {
                _dialogService.ShowToast(result.Message, "Success");
            }
        }

        [RelayCommand]
        private async Task DeleteAiTranslationConfig(string? configId)
        {
            if (string.IsNullOrWhiteSpace(configId)) return;

            await _mediator.Send(new DeleteAiTranslationConfigFeature.Command(configId));
        }

        [RelayCommand]
        private async Task TestAiTranslationConnection()
        {
            TranslationTestStatus = "测试中...";
            TranslationTestStatusColor = System.Windows.Media.Brushes.Gray;

            var result = await _mediator.Send(new TestExternalAiTranslationBatchFeature.Command());

            TranslationTestStatus = result.Message;
            TranslationTestStatusColor = result.Success && result.SuccessCount == result.TotalCount
                ? System.Windows.Media.Brushes.Green
                : result.Success && result.SuccessCount > 0
                    ? System.Windows.Media.Brushes.Orange
                    : System.Windows.Media.Brushes.Red;
        }

        public async void Receive(AiModelDeletedMessage message)
        {
            var result = await _mediator.Send(
                new HandleAiModelDeletedFeature.Command(message.DeletedModelName)
            );

            if (result.Success && result.AffectedConfigCount > 0)
            {
                OnPropertyChanged(nameof(Config));
                _dialogService.ShowToast(result.Message, "Warning");
            }
        }
    }
}

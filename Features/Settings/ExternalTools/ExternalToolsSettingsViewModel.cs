using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PromptMasterv6.Infrastructure.Services;
using PromptMasterv6.Core.Interfaces;
using PromptMasterv6.Features.Settings.AiModels;
using PromptMasterv6.Features.Settings.ApiCredentials;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Messaging;
using PromptMasterv6.Core.Messages;
using PromptMasterv6.Features.Main.FileManager.Messages;
using PromptMasterv6.Features.Settings.AiModels.Messages;
using PromptMasterv6.Features.Shared.Models;
using System.Linq;

namespace PromptMasterv6.Features.Settings.ExternalTools
{
    public partial class ExternalToolsSettingsViewModel : ObservableObject, IRecipient<AiModelDeletedMessage>
    {
        private readonly SettingsService _settingsService;
        private readonly ISessionState _sessionState;
        private readonly AiModelsViewModel _aiModelsVM;
        private readonly ApiCredentialsViewModel _apiCredentialsVM;
        private readonly DialogService _dialogService;
        private readonly SaveAiTranslationConfigFeature.Handler _saveAiTranslationConfigHandler;
        private readonly DeleteAiTranslationConfigFeature.Handler _deleteAiTranslationConfigHandler;

        public AppConfig Config => _settingsService.Config;
        public ObservableCollection<PromptItem> FilesView => _sessionState.Files;
        public AiModelsViewModel AiModelsVM => _aiModelsVM;
        public ApiCredentialsViewModel ApiCredentialsVM => _apiCredentialsVM;

        public global::PromptMasterv6.Features.ExternalTools.ExternalToolsViewModel ExternalToolsVM { get; }

        [ObservableProperty] private int selectedSubTab = 0;

        [RelayCommand]
        private void SelectSubTab(string tabIndexStr)
        {
            if (int.TryParse(tabIndexStr, out int tabIndex))
            {
                SelectedSubTab = tabIndex;
            }
        }

        public ExternalToolsSettingsViewModel(
            SettingsService settingsService,
            ISessionState sessionState,
            global::PromptMasterv6.Features.ExternalTools.ExternalToolsViewModel externalToolsVM,
            AiModelsViewModel aiModelsVM,
            ApiCredentialsViewModel apiCredentialsVM,
            DialogService dialogService,
            SaveAiTranslationConfigFeature.Handler saveAiTranslationConfigHandler,
            DeleteAiTranslationConfigFeature.Handler deleteAiTranslationConfigHandler)
        {
            _settingsService = settingsService;
            _sessionState = sessionState;
            ExternalToolsVM = externalToolsVM;
            _aiModelsVM = aiModelsVM;
            _apiCredentialsVM = apiCredentialsVM;
            _dialogService = dialogService;
            _saveAiTranslationConfigHandler = saveAiTranslationConfigHandler;
            _deleteAiTranslationConfigHandler = deleteAiTranslationConfigHandler;

            WeakReferenceMessenger.Default.Register(this);
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
        private void SaveAiTranslationConfig()
        {
            var promptId = Config.AiTranslationPromptId;
            var promptTitle = "";
            if (!string.IsNullOrWhiteSpace(promptId))
            {
                var msg = new RequestPromptFileMessage { PromptId = promptId };
                WeakReferenceMessenger.Default.Send(msg);
                promptTitle = msg.HasReceivedResponse ? msg.Response?.File?.Title ?? "" : "";
            }

            var result = _saveAiTranslationConfigHandler.Handle(new SaveAiTranslationConfigFeature.Command(
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
        private void DeleteAiTranslationConfig(string? configId)
        {
            if (string.IsNullOrWhiteSpace(configId)) return;

            _deleteAiTranslationConfigHandler.Handle(new DeleteAiTranslationConfigFeature.Command(configId));
        }

        public void Receive(AiModelDeletedMessage message)
        {
            var affectedConfigs = Config.SavedAiTranslationConfigs
                .Where(c => c.Model == message.DeletedModelName).ToList();

            if (affectedConfigs.Any())
            {
                foreach (var c in affectedConfigs)
                {
                    c.Model = "已失效 (请重新配置)";
                }
                _settingsService.SaveConfig();
                _dialogService.ShowToast($"警告：翻译引擎依赖的模型 '{message.DeletedModelName}' 已被删除，请重新配置。", "Warning");
            }
        }
    }
}

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PromptMasterv6.Infrastructure.Services;
using PromptMasterv6.Core.Interfaces;
using PromptMasterv6.Features.Settings.AiModels;
using PromptMasterv6.Features.Settings.ApiCredentials;
using System.Collections.ObjectModel;

namespace PromptMasterv6.Features.Settings.ExternalTools
{
    public partial class ExternalToolsSettingsViewModel : ObservableObject
    {
        private readonly SettingsService _settingsService;
        private readonly ISessionState _sessionState;
        private readonly AiModelsViewModel _aiModelsVM;
        private readonly ApiCredentialsViewModel _apiCredentialsVM;

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
            ApiCredentialsViewModel apiCredentialsVM)
        {
            _settingsService = settingsService;
            _sessionState = sessionState;
            ExternalToolsVM = externalToolsVM;
            _aiModelsVM = aiModelsVM;
            _apiCredentialsVM = apiCredentialsVM;
        }
    }
}

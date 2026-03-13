using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PromptMasterv6.Infrastructure.Services;
using MediatR;
using System.Threading.Tasks;

namespace PromptMasterv6.Features.Settings
{
    /// <summary>
    /// VSA 重构后的 SettingsViewModel - 仅负责 UI 状态管理
    /// 不再持有子 ViewModel 引用,符合垂直切片架构原则
    /// </summary>
    public partial class SettingsViewModel : ObservableObject
    {
        private readonly SettingsService _settingsService;
        private readonly LoggerService _logger;
        private readonly IMediator _mediator;

        #region Observable Properties - UI State

        [ObservableProperty] private bool isSettingsOpen;
        [ObservableProperty] private int selectedSettingsTab;

        #endregion

        #region Observable Properties - Config Access

        public AppConfig Config => _settingsService.Config;
        public LocalSettings LocalConfig => _settingsService.LocalConfig;

        #endregion

        public SettingsViewModel(
            SettingsService settingsService,
            LoggerService logger,
            IMediator mediator)
        {
            _settingsService = settingsService;
            _logger = logger;
            _mediator = mediator;

            _logger.LogInfo("SettingsViewModel initialized (VSA Refactored)", "SettingsViewModel.ctor");
        }

        #region Commands - Settings UI

        [RelayCommand]
        private void OpenSettings()
        {
            IsSettingsOpen = true;
        }

        [RelayCommand]
        private async Task CloseSettings()
        {
            IsSettingsOpen = false;
            await _mediator.Send(new CloseSettingsFeature.Command());
        }

        [RelayCommand]
        private async Task SelectSettingsTab(string tabIndexStr)
        {
            var result = await _mediator.Send(new SelectSettingsTabFeature.Command(tabIndexStr));
            if (result.Success)
            {
                SelectedSettingsTab = result.SelectedTabIndex;
            }
        }

        #endregion
    }
}

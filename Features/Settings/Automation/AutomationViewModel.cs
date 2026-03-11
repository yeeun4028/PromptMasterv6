using CommunityToolkit.Mvvm.ComponentModel;
using PromptMasterv6.Infrastructure.Services;
using PromptMasterv6.Features.Shared.Models;
using System.Collections.ObjectModel;

namespace PromptMasterv6.Features.Settings.Automation
{
    public partial class AutomationViewModel : ObservableObject
    {
        private readonly SettingsService _settingsService;

        [ObservableProperty] private ObservableCollection<WebTarget> _webDirectTargets;
        [ObservableProperty] private string _defaultWebTargetName;
        [ObservableProperty] private bool _enableDoubleEnterSend;

        public AutomationViewModel(SettingsService settingsService)
        {
            _settingsService = settingsService;
            
            var config = _settingsService.Config;
            _webDirectTargets = config.WebDirectTargets;
            _defaultWebTargetName = config.DefaultWebTargetName;
            _enableDoubleEnterSend = config.EnableDoubleEnterSend;
        }

        partial void OnDefaultWebTargetNameChanged(string value)
        {
            _settingsService.Config.DefaultWebTargetName = value;
        }

        partial void OnEnableDoubleEnterSendChanged(bool value)
        {
            _settingsService.Config.EnableDoubleEnterSend = value;
        }
    }
}

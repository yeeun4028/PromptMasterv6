using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PromptMasterv6.Infrastructure.Services;

namespace PromptMasterv6.Features.Settings.Proxy
{
    public partial class ProxyViewModel : ObservableObject
    {
        private readonly UpdateProxyFeature.Handler _updateProxyHandler;
        private readonly LoggerService _logger;

        [ObservableProperty] private string _proxyAddress;

        public ProxyViewModel(
            UpdateProxyFeature.Handler updateProxyHandler,
            LoggerService logger,
            SettingsService settingsService)
        {
            _updateProxyHandler = updateProxyHandler;
            _logger = logger;

            // 从配置加载初始值
            var config = settingsService.Config;
            _proxyAddress = config.ProxyAddress;
        }

        [RelayCommand]
        private async Task SaveProxySettings()
        {
            var command = new UpdateProxyFeature.Command(ProxyAddress);

            var result = await _updateProxyHandler.Handle(command, CancellationToken.None);

            if (result.Success)
            {
                _logger.LogInfo(result.Message, "ProxyViewModel.SaveProxySettings");
                // TODO: 显示成功提示 (Toast)
            }
            else
            {
                _logger.LogWarning(result.Message, "ProxyViewModel.SaveProxySettings");
                // TODO: 显示错误提示
            }
        }
    }
}

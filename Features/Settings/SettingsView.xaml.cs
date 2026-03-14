using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using PromptMasterv6.Features.AiModels;

namespace PromptMasterv6.Features.Settings
{
    /// <summary>
    /// VSA 重构后的 SettingsView - 通过 DI 容器直接解析子 View
    /// 不再依赖 SettingsViewModel 的聚合器模式
    /// </summary>
    public partial class SettingsView : System.Windows.Controls.UserControl
    {
        private readonly IServiceProvider? _serviceProvider;

        public SettingsView()
        {
            InitializeComponent();
            Loaded += SettingsView_Loaded;
        }

        // 通过构造函数注入 DI 容器
        public SettingsView(IServiceProvider serviceProvider) : this()
        {
            _serviceProvider = serviceProvider;
        }

        private void SettingsView_Loaded(object sender, RoutedEventArgs e)
        {
            // 动态加载子 View
            LoadChildViews();
        }

        private void LoadChildViews()
        {
            if (_serviceProvider == null) return;

            // 通过 DI 容器解析各个子 View
            // 每个子 View 会自动获得其对应的 ViewModel
            var shortcutView = _serviceProvider.GetService<Shortcut.ShortcutView>();
            var launcherView = _serviceProvider.GetService<Launcher.LauncherView>();
            var windowView = _serviceProvider.GetService<Window.WindowView>();
            var automationView = _serviceProvider.GetService<Automation.AutomationView>();
            var aiModelsView = _serviceProvider.GetService<AiModelsView>();
            var syncView = _serviceProvider.GetService<Sync.SyncView>();
            var externalToolsSettingsView = _serviceProvider.GetService<ExternalTools.ExternalToolsSettingsView>();
            var launchBarView = _serviceProvider.GetService<LaunchBar.LaunchBarView>();
            var proxyView = _serviceProvider.GetService<Proxy.ProxyView>();

            // 将子 View 添加到对应的容器中
            // 注意: 这里需要在 XAML 中为每个 Tab 添加一个容器控件
        }

        private SettingsViewModel? ViewModel => DataContext as SettingsViewModel;
    }
}

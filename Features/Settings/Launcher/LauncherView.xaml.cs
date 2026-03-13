using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;

namespace PromptMasterv6.Features.Settings.Launcher
{
    /// <summary>
    /// VSA 重构后的 LauncherView - 支持默认构造函数和 DI 注入
    /// </summary>
    public partial class LauncherView : System.Windows.Controls.UserControl
    {
        // 默认构造函数 - 供 XAML 使用
        public LauncherView()
        {
            var app = System.Windows.Application.Current as App;
            if (app?.ServiceProvider != null)
            {
                DataContext = app.ServiceProvider.GetService(typeof(LauncherSettingsViewModel));
            }
            InitializeComponent();
        }

        // DI 构造函数 - 供 DI 容器使用
        public LauncherView(LauncherSettingsViewModel viewModel)
        {
            DataContext = viewModel;
            InitializeComponent();
        }
    }
}

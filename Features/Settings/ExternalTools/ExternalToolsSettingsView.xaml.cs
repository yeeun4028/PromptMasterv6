using Microsoft.Extensions.DependencyInjection;

namespace PromptMasterv6.Features.Settings.ExternalTools
{
    /// <summary>
    /// VSA 重构后的 ExternalToolsSettingsView - 支持默认构造函数和 DI 注入
    /// </summary>
    public partial class ExternalToolsSettingsView : System.Windows.Controls.UserControl
    {
        // 默认构造函数 - 供 XAML 使用
        public ExternalToolsSettingsView()
        {
            var app = System.Windows.Application.Current as App;
            if (app?.ServiceProvider != null)
            {
                DataContext = app.ServiceProvider.GetService(typeof(ExternalToolsSettingsViewModel));
            }
            InitializeComponent();
        }

        // DI 构造函数 - 供 DI 容器使用
        public ExternalToolsSettingsView(ExternalToolsSettingsViewModel viewModel)
        {
            DataContext = viewModel;
            InitializeComponent();
        }
    }
}

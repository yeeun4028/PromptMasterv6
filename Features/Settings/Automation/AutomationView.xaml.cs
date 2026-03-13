using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;

namespace PromptMasterv6.Features.Settings.Automation
{
    /// <summary>
    /// VSA 重构后的 AutomationView - 支持默认构造函数和 DI 注入
    /// </summary>
    public partial class AutomationView : System.Windows.Controls.UserControl
    {
        // 默认构造函数 - 供 XAML 使用
        public AutomationView()
        {
            var app = System.Windows.Application.Current as App;
            if (app?.ServiceProvider != null)
            {
                DataContext = app.ServiceProvider.GetService(typeof(AutomationViewModel));
            }
            InitializeComponent();
        }

        // DI 构造函数 - 供 DI 容器使用
        public AutomationView(AutomationViewModel viewModel)
        {
            DataContext = viewModel;
            InitializeComponent();
        }
    }
}

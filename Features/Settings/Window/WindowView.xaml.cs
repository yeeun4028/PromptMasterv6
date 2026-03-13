using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;

namespace PromptMasterv6.Features.Settings.Window
{
    /// <summary>
    /// VSA 重构后的 WindowView - 支持默认构造函数和 DI 注入
    /// </summary>
    public partial class WindowView : System.Windows.Controls.UserControl
    {
        // 默认构造函数 - 供 XAML 使用
        public WindowView()
        {
            var app = System.Windows.Application.Current as App;
            if (app?.ServiceProvider != null)
            {
                DataContext = app.ServiceProvider.GetService(typeof(WindowViewModel));
            }
            InitializeComponent();
        }

        // DI 构造函数 - 供 DI 容器使用
        public WindowView(WindowViewModel viewModel)
        {
            DataContext = viewModel;
            InitializeComponent();
        }
    }
}

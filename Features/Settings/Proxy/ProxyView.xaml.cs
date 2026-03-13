using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;

namespace PromptMasterv6.Features.Settings.Proxy
{
    /// <summary>
    /// VSA 重构后的 ProxyView - 支持默认构造函数和 DI 注入
    /// </summary>
    public partial class ProxyView : System.Windows.Controls.UserControl
    {
        // 默认构造函数 - 供 XAML 使用
        public ProxyView()
        {
            var app = System.Windows.Application.Current as App;
            if (app?.ServiceProvider != null)
            {
                DataContext = app.ServiceProvider.GetService(typeof(ProxyViewModel));
            }
            InitializeComponent();
        }

        // DI 构造函数 - 供 DI 容器使用
        public ProxyView(ProxyViewModel viewModel)
        {
            DataContext = viewModel;
            InitializeComponent();
        }
    }
}

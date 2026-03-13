using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;

namespace PromptMasterv6.Features.Settings.Shortcut
{
    /// <summary>
    /// VSA 重构后的 ShortcutView - 支持默认构造函数和 DI 注入
    /// </summary>
    public partial class ShortcutView : System.Windows.Controls.UserControl
    {
        // 默认构造函数 - 供 XAML 使用
        public ShortcutView()
        {
            // 在 InitializeComponent 之前设置 DataContext
            var app = System.Windows.Application.Current as App;
            if (app?.ServiceProvider != null)
            {
                DataContext = app.ServiceProvider.GetService(typeof(ShortcutViewModel));
            }

            InitializeComponent();
        }

        // DI 构造函数 - 供 DI 容器使用
        public ShortcutView(ShortcutViewModel viewModel)
        {
            DataContext = viewModel;
            InitializeComponent();
        }
    }
}

using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;

namespace PromptMasterv6.Features.Settings.AiModels
{
    /// <summary>
    /// VSA 重构后的 AiModelsView - 支持默认构造函数和 DI 注入
    /// </summary>
    public partial class AiModelsView : System.Windows.Controls.UserControl
    {
        // 默认构造函数 - 供 XAML 使用
        public AiModelsView()
        {
            var app = System.Windows.Application.Current as App;
            if (app?.ServiceProvider != null)
            {
                DataContext = app.ServiceProvider.GetService(typeof(AiModelsViewModel));
            }
            InitializeComponent();
        }

        // DI 构造函数 - 供 DI 容器使用
        public AiModelsView(AiModelsViewModel viewModel)
        {
            DataContext = viewModel;
            InitializeComponent();
        }
    }
}

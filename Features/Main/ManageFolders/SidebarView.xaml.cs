using Microsoft.Extensions.DependencyInjection;

namespace PromptMasterv6.Features.Main.Sidebar
{
    public partial class SidebarView : System.Windows.Controls.UserControl
    {
        public SidebarView() : this(App.Services.GetRequiredService<SidebarViewModel>())
        {
        }

        public SidebarView(SidebarViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}

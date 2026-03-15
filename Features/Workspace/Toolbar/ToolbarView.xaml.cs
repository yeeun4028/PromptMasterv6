using Microsoft.Extensions.DependencyInjection;

namespace PromptMasterv6.Features.Workspace.Toolbar;

public partial class ToolbarView : System.Windows.Controls.UserControl
{
    public ToolbarView()
    {
        InitializeComponent();
        DataContext = App.Services.GetRequiredService<ToolbarViewModel>();
    }
}

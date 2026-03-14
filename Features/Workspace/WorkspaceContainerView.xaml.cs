using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;

namespace PromptMasterv6.Features.Workspace;

public partial class WorkspaceContainerView : System.Windows.Controls.UserControl
{
    public WorkspaceContainerView(WorkspaceContainerViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}

using Microsoft.Extensions.DependencyInjection;
using PromptMasterv6.Features.Workspace.State;

namespace PromptMasterv6.Features.Workspace.Editor;

public partial class EditorView : System.Windows.Controls.UserControl
{
    public EditorView()
    {
        InitializeComponent();
        DataContext = App.Services.GetRequiredService<IWorkspaceState>();
    }
}

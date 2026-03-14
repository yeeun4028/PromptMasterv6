using Microsoft.Extensions.DependencyInjection;
using System.Windows.Controls;

namespace PromptMasterv6.Features.Workspace
{
    public partial class WorkspaceView : System.Windows.Controls.UserControl
    {
        public WorkspaceView()
        {
            InitializeComponent();
            DataContext = App.Services.GetRequiredService<WorkspaceViewModel>();
            _ = ((WorkspaceViewModel)DataContext).InitializeAsync();
        }
    }
}

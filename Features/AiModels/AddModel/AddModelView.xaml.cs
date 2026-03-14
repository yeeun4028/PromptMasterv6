using System.Windows.Controls;

namespace PromptMasterv6.Features.AiModels.AddModel;

public partial class AddModelView : System.Windows.Controls.UserControl
{
    public AddModelView(AddModelViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}

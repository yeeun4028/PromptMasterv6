using System.Windows.Controls;

namespace PromptMasterv6.Features.AiModels.AiModelList;

public partial class AiModelListView : System.Windows.Controls.UserControl
{
    public AiModelListView(AiModelListViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}

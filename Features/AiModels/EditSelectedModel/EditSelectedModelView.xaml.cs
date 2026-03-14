using System.Windows.Controls;

namespace PromptMasterv6.Features.AiModels.EditSelectedModel;

public partial class EditSelectedModelView : System.Windows.Controls.UserControl
{
    public EditSelectedModelView(EditSelectedModelViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        
        Unloaded += OnUnloaded;
    }

    private void OnUnloaded(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is EditSelectedModelViewModel viewModel)
        {
            viewModel.Dispose();
        }
        Unloaded -= OnUnloaded;
    }
}

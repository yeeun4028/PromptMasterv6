using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;

namespace PromptMasterv6.Features.AiModels;

public partial class AiModelsView : System.Windows.Controls.UserControl
{
    public AiModelsView()
    {
        var app = System.Windows.Application.Current as App;
        if (app?.ServiceProvider != null)
        {
            DataContext = app.ServiceProvider.GetService(typeof(AiModelsViewModel)) as AiModelsViewModel;
        }

        InitializeComponent();
    }

    public AiModelsView(AiModelsViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();
    }
}

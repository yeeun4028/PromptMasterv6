using System.Windows.Controls;
using PromptMasterv6.Features.Shared.Models;

namespace PromptMasterv6.Features.AiModels.TestConnection;

public partial class TestConnectionView : System.Windows.Controls.UserControl
{
    public static readonly System.Windows.DependencyProperty SelectedModelProperty =
        System.Windows.DependencyProperty.Register(
            nameof(SelectedModel), 
            typeof(AiModelConfig), 
            typeof(TestConnectionView),
            new System.Windows.PropertyMetadata(null));

    public AiModelConfig? SelectedModel
    {
        get => (AiModelConfig?)GetValue(SelectedModelProperty);
        set => SetValue(SelectedModelProperty, value);
    }

    public TestConnectionView()
    {
        InitializeComponent();
    }

    public TestConnectionView(TestConnectionViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }
}

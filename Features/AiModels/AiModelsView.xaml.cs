using System.Windows.Controls;
using PromptMasterv6.Features.AiModels.AddModel;
using PromptMasterv6.Features.AiModels.AiModelList;
using PromptMasterv6.Features.AiModels.EditSelectedModel;
using PromptMasterv6.Features.AiModels.Shared;
using PromptMasterv6.Infrastructure.Services;

namespace PromptMasterv6.Features.AiModels;

public partial class AiModelsView : System.Windows.Controls.UserControl
{
    public AiModelsView(
        AddModelView addModelView,
        AiModelListView aiModelListView,
        EditSelectedModelView editSelectedModelView,
        AiModelSelectionState selectionState,
        SettingsService settingsService)
    {
        InitializeComponent();
        
        EditSelectedModelContainer.Content = editSelectedModelView;
        AddModelContainer.Content = addModelView;
        AiModelListContainer.Content = aiModelListView;
        
        DataContext = new { Config = settingsService.Config, SelectionState = selectionState };
    }
}

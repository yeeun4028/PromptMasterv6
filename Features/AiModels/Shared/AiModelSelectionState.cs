using CommunityToolkit.Mvvm.ComponentModel;
using PromptMasterv6.Features.Shared.Models;

namespace PromptMasterv6.Features.AiModels.Shared;

public partial class AiModelSelectionState : ObservableObject
{
    [ObservableProperty] private AiModelConfig? _selectedModel;

    public event Action<AiModelConfig?>? ModelSelected;

    public void SelectModel(AiModelConfig? model)
    {
        SelectedModel = model;
        ModelSelected?.Invoke(model);
    }
}

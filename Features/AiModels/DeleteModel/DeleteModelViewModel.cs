using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using PromptMasterv6.Features.Shared.Models;

namespace PromptMasterv6.Features.AiModels.DeleteModel;

public partial class DeleteModelViewModel : ObservableObject
{
    private readonly IMediator _mediator;

    public DeleteModelViewModel(IMediator mediator)
    {
        _mediator = mediator;
    }

    [RelayCommand]
    private void ConfirmDeleteModel(AiModelConfig? model)
    {
        if (model == null) return;
        model.IsPendingDelete = true;
    }

    [RelayCommand]
    private async Task DeleteModel(AiModelConfig? model)
    {
        if (model == null) return;
        await _mediator.Send(new DeleteAiModelFeature.Command(model));
    }
}

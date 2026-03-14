using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using PromptMasterv6.Features.AiModels.DeleteModel;
using PromptMasterv6.Features.Shared.Models;
using System.Threading.Tasks;

namespace PromptMasterv6.Features.AiModels;

public partial class DeleteAiModelViewModel : ObservableObject
{
    private readonly IMediator _mediator;

    [ObservableProperty] private AiModelConfig? _deletedModel;

    public DeleteAiModelViewModel(IMediator mediator)
    {
        _mediator = mediator;
    }

    [RelayCommand]
    private async Task ExecuteAsync(AiModelConfig? model)
    {
        if (model == null) return;

        var result = await _mediator.Send(new DeleteAiModelFeature.Command(model));

        if (result.Success)
        {
            DeletedModel = model;
        }
    }

    [RelayCommand]
    private void ConfirmDelete(AiModelConfig? model)
    {
        if (model == null) return;
        model.IsPendingDelete = true;
    }
}

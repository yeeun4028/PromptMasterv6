using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using PromptMasterv6.Features.AiModels.RenameModel;
using PromptMasterv6.Features.Shared.Models;
using PromptMasterv6.Infrastructure.Services;

namespace PromptMasterv6.Features.AiModels.RenameModel;

public partial class RenameModelViewModel : ObservableObject
{
    private readonly IMediator _mediator;
    private readonly DialogService _dialogService;

    public RenameModelViewModel(IMediator mediator, DialogService dialogService)
    {
        _mediator = mediator;
        _dialogService = dialogService;
    }

    [RelayCommand]
    private async Task RenameModel(AiModelConfig? model, CancellationToken cancellationToken)
    {
        if (model == null) return;

        var initialName = string.IsNullOrWhiteSpace(model.Remark) ? model.ModelName : model.Remark;
        var (confirmed, resultName) = _dialogService.ShowNameInputDialog(initialName);

        if (confirmed && !string.IsNullOrWhiteSpace(resultName))
        {
            var result = await _mediator.Send(new RenameAiModelFeature.Command(model.Id, resultName), cancellationToken);

            if (!result.Success)
            {
                _dialogService.ShowToast(result.Message, "Error");
            }
        }
    }
}

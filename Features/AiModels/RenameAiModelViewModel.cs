using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using PromptMasterv6.Features.AiModels.RenameModel;
using PromptMasterv6.Infrastructure.Services;
using PromptMasterv6.Features.Shared.Models;
using System.Threading.Tasks;

namespace PromptMasterv6.Features.AiModels;

public partial class RenameAiModelViewModel : ObservableObject
{
    private readonly IMediator _mediator;
    private readonly DialogService _dialogService;

    public RenameAiModelViewModel(IMediator mediator, DialogService dialogService)
    {
        _mediator = mediator;
        _dialogService = dialogService;
    }

    [RelayCommand]
    private async Task ExecuteAsync(AiModelConfig? model)
    {
        if (model == null) return;

        string initialName = string.IsNullOrWhiteSpace(model.Remark) ? model.ModelName : model.Remark;
        var (confirmed, resultName) = _dialogService.ShowNameInputDialog(initialName);

        if (confirmed && !string.IsNullOrWhiteSpace(resultName))
        {
            var result = await _mediator.Send(new RenameAiModelFeature.Command(model.Id, resultName));

            if (!result.Success)
            {
                _dialogService.ShowToast(result.Message, "Error");
            }
        }
    }
}

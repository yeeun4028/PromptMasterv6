using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using PromptMasterv6.Infrastructure.Services;

namespace PromptMasterv6.Features.AiModels.AddModel;

public partial class AddModelViewModel : ObservableObject
{
    private readonly IMediator _mediator;
    private readonly DialogService _dialogService;

    public AddModelViewModel(IMediator mediator, DialogService dialogService)
    {
        _mediator = mediator;
        _dialogService = dialogService;
    }

    [RelayCommand]
    private async Task AddModel(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new AddAiModelFeature.Command(), cancellationToken);

        if (!result.Success)
        {
            _dialogService.ShowToast(result.Message, "Error");
        }
    }
}

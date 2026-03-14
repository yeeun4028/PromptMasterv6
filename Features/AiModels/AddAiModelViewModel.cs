using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using PromptMasterv6.Features.AiModels.AddModel;
using PromptMasterv6.Infrastructure.Services;
using PromptMasterv6.Features.Shared.Models;
using System.Threading.Tasks;

namespace PromptMasterv6.Features.AiModels;

public partial class AddAiModelViewModel : ObservableObject
{
    private readonly IMediator _mediator;
    private readonly DialogService _dialogService;

    [ObservableProperty] private AiModelConfig? _addedModel;

    public AddAiModelViewModel(IMediator mediator, DialogService dialogService)
    {
        _mediator = mediator;
        _dialogService = dialogService;
    }

    [RelayCommand]
    private async Task ExecuteAsync()
    {
        var result = await _mediator.Send(new AddAiModelFeature.Command());

        if (result.Success && result.AddedModel != null)
        {
            AddedModel = result.AddedModel;
        }
        else
        {
            _dialogService.ShowToast(result.Message, "Error");
        }
    }
}

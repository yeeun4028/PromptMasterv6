using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using PromptMasterv6.Features.AiModels.DeleteModel;
using PromptMasterv6.Features.AiModels.Events;
using PromptMasterv6.Features.AiModels.RenameModel;
using PromptMasterv6.Features.AiModels.Shared;
using PromptMasterv6.Features.Shared.Models;
using PromptMasterv6.Infrastructure.Services;

namespace PromptMasterv6.Features.AiModels.AiModelList;

public partial class AiModelListViewModel : ObservableObject, 
    INotificationHandler<AiModelAddedEvent>, 
    INotificationHandler<AiModelDeletedEvent>
{
    private readonly SettingsService _settingsService;
    private readonly AiModelSelectionState _selectionState;
    private readonly IMediator _mediator;

    public AppConfig Config => _settingsService.Config;

    public AiModelConfig? SelectedModel
    {
        get => _selectionState.SelectedModel;
        set => _selectionState.SelectModel(value);
    }

    public AiModelListViewModel(
        SettingsService settingsService,
        AiModelSelectionState selectionState,
        IMediator mediator)
    {
        _settingsService = settingsService;
        _selectionState = selectionState;
        _mediator = mediator;
        _selectionState.ModelSelected += OnModelSelected;
    }

    private void OnModelSelected(AiModelConfig? model)
    {
        OnPropertyChanged(nameof(SelectedModel));
    }

    [RelayCommand]
    private void DeleteModel(AiModelConfig? model)
    {
        if (model == null) return;
        model.IsPendingDelete = true;
    }

    [RelayCommand]
    private async Task ConfirmDeleteModel(AiModelConfig? model, CancellationToken cancellationToken)
    {
        if (model == null) return;
        await _mediator.Send(new DeleteAiModelFeature.Command(model), cancellationToken);
    }

    public Task Handle(AiModelAddedEvent notification, CancellationToken cancellationToken)
    {
        _selectionState.SelectModel(notification.AddedModel);
        OnPropertyChanged(nameof(Config));
        return Task.CompletedTask;
    }

    public Task Handle(AiModelDeletedEvent notification, CancellationToken cancellationToken)
    {
        if (_selectionState.SelectedModel == notification.DeletedModel)
        {
            _selectionState.SelectModel(null);
        }
        return Task.CompletedTask;
    }
}

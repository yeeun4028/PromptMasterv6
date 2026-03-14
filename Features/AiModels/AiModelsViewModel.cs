using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using PromptMasterv6.Infrastructure.Services;
using PromptMasterv6.Features.Shared.Models;
using PromptMasterv6.Features.AiModels.TestTranslationBatch;
using PromptMasterv6.Features.AiModels.AddModel;
using PromptMasterv6.Features.AiModels.DeleteModel;
using PromptMasterv6.Features.AiModels.RenameModel;
using PromptMasterv6.Features.AiModels.TestConnection;
using PromptMasterv6.Features.AiModels.Events;
using System.Threading.Tasks;
using MediaBrush = System.Windows.Media.Brush;
using MediaBrushes = System.Windows.Media.Brushes;

namespace PromptMasterv6.Features.AiModels;

public partial class AiModelsViewModel : ObservableObject, INotificationHandler<AiModelAddedEvent>, INotificationHandler<AiModelDeletedEvent>
{
    private readonly SettingsService _settingsService;
    private readonly IMediator _mediator;
    private readonly DialogService _dialogService;

    [ObservableProperty] private AiModelConfig? _selectedSavedModel;
    [ObservableProperty] private string? _translationTestStatus;
    [ObservableProperty] private MediaBrush _translationTestStatusColor = MediaBrushes.Gray;
    [ObservableProperty] private string? _connectionTestStatus;
    [ObservableProperty] private MediaBrush _connectionTestStatusColor = MediaBrushes.Gray;

    public AppConfig Config => _settingsService.Config;

    public AiModelsViewModel(SettingsService settingsService, IMediator mediator, DialogService dialogService)
    {
        _settingsService = settingsService;
        _mediator = mediator;
        _dialogService = dialogService;
    }

    [RelayCommand]
    private async Task AddModel()
    {
        var result = await _mediator.Send(new AddAiModelFeature.Command());

        if (!result.Success)
        {
            _dialogService.ShowToast(result.Message, "Error");
        }
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

    [RelayCommand]
    private async Task RenameModel(AiModelConfig? model)
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

    [RelayCommand]
    private async Task TestConnection(AiModelConfig? model)
    {
        if (model == null) return;

        ConnectionTestStatus = "测试中...";
        ConnectionTestStatusColor = MediaBrushes.Gray;

        var cmd = new TestAiConnectionFeature.Command(
            model.ApiKey, model.BaseUrl, model.ModelName, model.UseProxy);

        var result = await _mediator.Send(cmd);

        ConnectionTestStatus = result.Success && result.ResponseTimeMs.HasValue
            ? $"{result.Message} ({result.ResponseTimeMs}ms)"
            : result.Message;
        ConnectionTestStatusColor = result.Success ? MediaBrushes.Green : MediaBrushes.Red;
    }

    [RelayCommand]
    private async Task TestAiTranslationConnection()
    {
        TranslationTestStatus = "测试中...";
        TranslationTestStatusColor = MediaBrushes.Gray;

        var result = await _mediator.Send(new TestAiTranslationBatchFeature.Command());

        TranslationTestStatus = result.Message;
        TranslationTestStatusColor = result.Success && result.SuccessCount == result.TotalCount
            ? MediaBrushes.Green
            : result.Success && result.SuccessCount > 0
                ? MediaBrushes.Orange
                : MediaBrushes.Red;
    }

    public Task Handle(AiModelAddedEvent notification, CancellationToken cancellationToken)
    {
        SelectedSavedModel = notification.AddedModel;
        OnPropertyChanged(nameof(Config));
        return Task.CompletedTask;
    }

    public Task Handle(AiModelDeletedEvent notification, CancellationToken cancellationToken)
    {
        if (SelectedSavedModel == notification.DeletedModel)
        {
            SelectedSavedModel = null;
        }
        return Task.CompletedTask;
    }
}

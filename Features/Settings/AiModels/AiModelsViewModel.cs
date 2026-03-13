using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using PromptMasterv6.Infrastructure.Services;
using PromptMasterv6.Features.Shared.Models;
using PromptMasterv6.Features.Settings.AiModels.AddAiModel;
using PromptMasterv6.Features.Settings.AiModels.RenameAiModel;
using System.Threading.Tasks;

namespace PromptMasterv6.Features.Settings.AiModels;

public partial class AiModelsViewModel : ObservableObject
{
    private readonly SettingsService _settingsService;
    private readonly IMediator _mediator;
    private readonly DialogService _dialogService;

    [ObservableProperty] private string? testStatus;
    [ObservableProperty] private System.Windows.Media.Brush testStatusColor = System.Windows.Media.Brushes.Gray;
    [ObservableProperty] private string? translationTestStatus;
    [ObservableProperty] private System.Windows.Media.Brush translationTestStatusColor = System.Windows.Media.Brushes.Gray;
    [ObservableProperty] private AiModelConfig? selectedSavedModel;

    public AppConfig Config => _settingsService.Config;

    public AiModelsViewModel(
        SettingsService settingsService,
        IMediator mediator,
        DialogService dialogService)
    {
        _settingsService = settingsService;
        _mediator = mediator;
        _dialogService = dialogService;
    }

    [RelayCommand]
    private async Task TestAiConnection(AiModelConfig? model)
    {
        if (model == null) return;

        TestStatus = "测试中...";
        TestStatusColor = System.Windows.Media.Brushes.Gray;

        var cmd = new TestAiConnectionFeature.Command(
            model.ApiKey, model.BaseUrl, model.ModelName, model.UseProxy);

        var result = await _mediator.Send(cmd);

        TestStatus = result.Success && result.ResponseTimeMs.HasValue 
            ? $"{result.Message} ({result.ResponseTimeMs}ms)" 
            : result.Message;
        TestStatusColor = result.Success ? System.Windows.Media.Brushes.Green : System.Windows.Media.Brushes.Red;
    }

    [RelayCommand]
    private async Task TestAiTranslationConnection()
    {
        TranslationTestStatus = "测试中...";
        TranslationTestStatusColor = System.Windows.Media.Brushes.Gray;

        var result = await _mediator.Send(new TestAiTranslationBatchFeature.Command());

        TranslationTestStatus = result.Message;
        TranslationTestStatusColor = result.Success && result.SuccessCount == result.TotalCount
            ? System.Windows.Media.Brushes.Green
            : result.Success && result.SuccessCount > 0
                ? System.Windows.Media.Brushes.Orange
                : System.Windows.Media.Brushes.Red;
    }

    [RelayCommand]
    private async Task AddAiModel()
    {
        var result = await _mediator.Send(new AddAiModelFeature.Command());

        if (result.Success && result.AddedModel != null)
        {
            SelectedSavedModel = result.AddedModel;
            OnPropertyChanged(nameof(Config));
        }
        else
        {
            _dialogService.ShowToast(result.Message, "Error");
        }
    }

    [RelayCommand]
    private void ConfirmDeleteAiModel(AiModelConfig? model)
    {
        if (model == null) return;
        model.IsPendingDelete = true;
    }

    [RelayCommand]
    private async Task DeleteAiModel(AiModelConfig? model)
    {
        if (model == null) return;

        var result = await _mediator.Send(new DeleteAiModelFeature.Command(model));

        if (result.Success)
        {
            if (SelectedSavedModel == model)
            {
                SelectedSavedModel = null;
            }
        }
    }

    [RelayCommand]
    private async Task RenameAiModel(AiModelConfig? model)
    {
        if (model == null) return;

        string initialName = string.IsNullOrWhiteSpace(model.Remark) ? model.ModelName : model.Remark;
        var (confirmed, resultName) = _dialogService.ShowNameInputDialog(initialName);
        
        if (confirmed && !string.IsNullOrWhiteSpace(resultName))
        {
            var result = await _mediator.Send(new RenameAiModelFeature.Command(model.Id, resultName));

            if (result.Success)
            {
                OnPropertyChanged(nameof(Config));
            }
            else
            {
                _dialogService.ShowToast(result.Message, "Error");
            }
        }
    }
}

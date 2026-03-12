using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PromptMasterv6.Infrastructure.Services;
using PromptMasterv6.Features.Shared.Models;
using PromptMasterv6.Features.Settings.AiModels.AddAiModel;
using PromptMasterv6.Features.Settings.AiModels.RenameAiModel;
using PromptMasterv6.Features.Shared.Dialogs;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace PromptMasterv6.Features.Settings.AiModels;

public partial class AiModelsViewModel : ObservableObject
{
    private readonly SettingsService _settingsService;
    private readonly TestAiConnectionFeature.Handler _testHandler;
    private readonly DeleteAiModelFeature.Handler _deleteHandler;
    private readonly AddAiModelFeature.Handler _addAiModelHandler;
    private readonly RenameAiModelFeature.Handler _renameAiModelHandler;
    private readonly DialogService _dialogService;

    [ObservableProperty] private string? testStatus;
    [ObservableProperty] private System.Windows.Media.Brush testStatusColor = System.Windows.Media.Brushes.Gray;
    [ObservableProperty] private string? translationTestStatus;
    [ObservableProperty] private System.Windows.Media.Brush translationTestStatusColor = System.Windows.Media.Brushes.Gray;
    [ObservableProperty] private AiModelConfig? selectedSavedModel;

    public AppConfig Config => _settingsService.Config;

    public AiModelsViewModel(
        SettingsService settingsService,
        TestAiConnectionFeature.Handler testHandler,
        DeleteAiModelFeature.Handler deleteHandler,
        AddAiModelFeature.Handler addAiModelHandler,
        RenameAiModelFeature.Handler renameAiModelHandler,
        DialogService dialogService)
    {
        _settingsService = settingsService;
        _testHandler = testHandler;
        _deleteHandler = deleteHandler;
        _addAiModelHandler = addAiModelHandler;
        _renameAiModelHandler = renameAiModelHandler;
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

        var result = await _testHandler.Handle(cmd);

        TestStatus = result.Success && result.ResponseTimeMs.HasValue 
            ? $"{result.Message} ({result.ResponseTimeMs}ms)" 
            : result.Message;
        TestStatusColor = result.Success ? System.Windows.Media.Brushes.Green : System.Windows.Media.Brushes.Red;
    }

    [RelayCommand]
    private async Task TestAiTranslationConnection()
    {
        var enabledModels = Config.SavedModels.Where(m => m.IsEnableForTranslation).ToList();
        if (enabledModels.Count == 0)
        {
            TranslationTestStatus = "请先勾选至少一个参与翻译的 AI 模型";
            TranslationTestStatusColor = System.Windows.Media.Brushes.Red;
            return;
        }

        TranslationTestStatus = "测试中...";
        TranslationTestStatusColor = System.Windows.Media.Brushes.Gray;

        int successCount = 0;
        string lastError = "";
        long totalTime = 0;

        foreach (var model in enabledModels)
        {
            var cmd = new TestAiConnectionFeature.Command(
                model.ApiKey, model.BaseUrl, model.ModelName, model.UseProxy);
            var result = await _testHandler.Handle(cmd);
            
            if (result.Success)
            {
                successCount++;
                if (result.ResponseTimeMs.HasValue)
                    totalTime += result.ResponseTimeMs.Value;
            }
            else lastError = result.Message;
        }

        if (successCount == enabledModels.Count)
        {
            var avgTime = successCount > 0 ? totalTime / successCount : 0;
            TranslationTestStatus = $"全部 {successCount} 个模型连接成功 (平均 {avgTime}ms)";
            TranslationTestStatusColor = System.Windows.Media.Brushes.Green;
        }
        else if (successCount > 0)
        {
            TranslationTestStatus = $"部分成功 ({successCount}/{enabledModels.Count})\n失败示例: {lastError}";
            TranslationTestStatusColor = System.Windows.Media.Brushes.Orange;
        }
        else
        {
            TranslationTestStatus = $"全部失败。\n错误示例: {lastError}";
            TranslationTestStatusColor = System.Windows.Media.Brushes.Red;
        }
    }

    [RelayCommand]
    private async Task AddAiModel()
    {
        var result = await _addAiModelHandler.Handle(new AddAiModelFeature.Command());

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
    private void DeleteAiModel(AiModelConfig? model)
    {
        if (model == null) return;

        var cmd = new DeleteAiModelFeature.Command(model);
        _deleteHandler.Handle(cmd);

        if (SelectedSavedModel == model)
        {
            SelectedSavedModel = null;
        }
    }

    [RelayCommand]
    private async Task RenameAiModel(AiModelConfig? model)
    {
        if (model == null) return;

        string initialName = string.IsNullOrWhiteSpace(model.Remark) ? model.ModelName : model.Remark;
        var dialog = new NameInputDialog(initialName);
        if (dialog.ShowDialog() == true)
        {
            var result = await _renameAiModelHandler.Handle(new RenameAiModelFeature.Command(model.Id, dialog.ResultName));

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

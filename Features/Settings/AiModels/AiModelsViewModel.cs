using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PromptMasterv6.Core.Interfaces;
using PromptMasterv6.Infrastructure.Services;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace PromptMasterv6.Features.Settings.AiModels;

public partial class AiModelsViewModel : ObservableObject
{
    private readonly ISettingsService _settingsService;
    private readonly IAiService _aiService;

    [ObservableProperty] private string? testStatus;
    [ObservableProperty] private System.Windows.Media.Brush testStatusColor = System.Windows.Media.Brushes.Gray;
    [ObservableProperty] private string? translationTestStatus;
    [ObservableProperty] private System.Windows.Media.Brush translationTestStatusColor = System.Windows.Media.Brushes.Gray;
    [ObservableProperty] private AiModelConfig? selectedSavedModel;

    public AppConfig Config => _settingsService.Config;

    public AiModelsViewModel(ISettingsService settingsService, IAiService aiService)
    {
        _settingsService = settingsService;
        _aiService = aiService;
    }

    [RelayCommand]
    private async Task TestAiConnection(AiModelConfig? model)
    {
        if (model == null)
        {
            var (success, msg, timeMs) = await _aiService.TestConnectionAsync(Config);
            TestStatus = success && timeMs.HasValue ? $"{msg} ({timeMs}ms)" : msg;
            TestStatusColor = success ? System.Windows.Media.Brushes.Green : System.Windows.Media.Brushes.Red;
            return;
        }

        TestStatus = "测试中...";
        TestStatusColor = System.Windows.Media.Brushes.Gray;
        var (success2, message, responseTimeMs) = await _aiService.TestConnectionAsync(model.ApiKey, model.BaseUrl, model.ModelName, model.UseProxy);
        TestStatus = success2 && responseTimeMs.HasValue ? $"{message} ({responseTimeMs}ms)" : message;
        TestStatusColor = success2 ? System.Windows.Media.Brushes.Green : System.Windows.Media.Brushes.Red;
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
            var result = await _aiService.TestConnectionAsync(model.ApiKey, model.BaseUrl, model.ModelName, model.UseProxy);
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
    private void AddAiModel()
    {
        var newModel = new AiModelConfig
        {
            Id = Guid.NewGuid().ToString(),
            ModelName = "gpt-3.5-turbo",
            BaseUrl = "https://api.openai.com/v1",
            ApiKey = "",
            Remark = "New Model"
        };

        Config.SavedModels.Insert(0, newModel);
        SelectedSavedModel = newModel;
        
        if (string.IsNullOrEmpty(Config.ActiveModelId))
        {
            Config.ActiveModelId = newModel.Id;
        }
        
        _settingsService.SaveConfig();
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
        var idx = Config.SavedModels.IndexOf(model);
        if (idx >= 0) Config.SavedModels.RemoveAt(idx);
        
        if (Config.ActiveModelId == model.Id) Config.ActiveModelId = "";
        if (SelectedSavedModel == model) SelectedSavedModel = null;
        
        _settingsService.SaveConfig();
    }

    [RelayCommand]
    private void RenameAiModel(AiModelConfig? model)
    {
        if (model == null) return;

        string initialName = string.IsNullOrWhiteSpace(model.Remark) ? model.ModelName : model.Remark;
        var dialog = new NameInputDialog(initialName);
        if (dialog.ShowDialog() == true)
        {
            model.Remark = dialog.ResultName;
            _settingsService.SaveConfig();
        }
    }
}

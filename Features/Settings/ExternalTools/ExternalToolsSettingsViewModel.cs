using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using PromptMasterv6.Infrastructure.Services;
using PromptMasterv6.Core.Interfaces;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Messaging;
using PromptMasterv6.Core.Messages;
using PromptMasterv6.Features.Main.FileManager.Messages;
using PromptMasterv6.Features.Settings.AiModels.Messages;
using PromptMasterv6.Features.Shared.Models;
using PromptMasterv6.Features.Settings.ExternalTools.HandleAiModelDeleted;
using System.Linq;

namespace PromptMasterv6.Features.Settings.ExternalTools
{
    public partial class ExternalToolsSettingsViewModel : ObservableObject, IRecipient<AiModelDeletedMessage>
    {
        private readonly SettingsService _settingsService;
        private readonly ISessionState _sessionState;
        private readonly DialogService _dialogService;
        private readonly IMediator _mediator;

        public AppConfig Config => _settingsService.Config;
        public ObservableCollection<PromptItem> FilesView => _sessionState.Files;

        public global::PromptMasterv6.Features.ExternalTools.ExternalToolsViewModel ExternalToolsVM { get; }

        [ObservableProperty] private int selectedSubTab = 0;
        [ObservableProperty] private string? translationTestStatus;
        [ObservableProperty] private System.Windows.Media.Brush translationTestStatusColor = System.Windows.Media.Brushes.Gray;

        [RelayCommand]
        private async Task SelectSubTab(string tabIndexStr)
        {
            var result = await _mediator.Send(new SelectSubTabFeature.Command(tabIndexStr));
            if (result.Success)
            {
                SelectedSubTab = result.SelectedTabIndex;
            }
        }

        public ExternalToolsSettingsViewModel(
            SettingsService settingsService,
            ISessionState sessionState,
            global::PromptMasterv6.Features.ExternalTools.ExternalToolsViewModel externalToolsVM,
            DialogService dialogService,
            IMediator mediator)
        {
            _settingsService = settingsService;
            _sessionState = sessionState;
            ExternalToolsVM = externalToolsVM;
            _dialogService = dialogService;
            _mediator = mediator;

            WeakReferenceMessenger.Default.Register(this);
        }

        [RelayCommand]
        private void JumpToEditPrompt()
        {
            var promptId = Config.AiTranslationPromptId;
            if (string.IsNullOrWhiteSpace(promptId)) return;

            var msg = new RequestPromptFileMessage { PromptId = promptId };
            WeakReferenceMessenger.Default.Send(msg);
            if (msg.HasReceivedResponse && msg.Response != null && msg.Response.File != null)
            {
                WeakReferenceMessenger.Default.Send(new JumpToEditPromptMessage { File = msg.Response.File });
            }
        }

        [RelayCommand]
        private async Task SaveAiTranslationConfig()
        {
            var promptId = Config.AiTranslationPromptId;
            var promptTitle = "";
            if (!string.IsNullOrWhiteSpace(promptId))
            {
                var msg = new RequestPromptFileMessage { PromptId = promptId };
                WeakReferenceMessenger.Default.Send(msg);
                promptTitle = msg.HasReceivedResponse ? msg.Response?.File?.Title ?? "" : "";
            }

            var result = await _mediator.Send(new SaveAiTranslationConfigFeature.Command(
                promptId,
                promptTitle,
                Config.AiBaseUrl,
                Config.AiApiKey,
                Config.AiModel
            ));

            if (result.Success)
            {
                _dialogService.ShowToast(result.Message, "Success");
            }
        }

        [RelayCommand]
        private async Task DeleteAiTranslationConfig(string? configId)
        {
            if (string.IsNullOrWhiteSpace(configId)) return;

            await _mediator.Send(new DeleteAiTranslationConfigFeature.Command(configId));
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
                var cmd = new Features.Settings.AiModels.TestAiConnectionFeature.Command(
                    model.ApiKey, model.BaseUrl, model.ModelName, model.UseProxy);
                var result = await _mediator.Send(cmd);

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

        public async void Receive(AiModelDeletedMessage message)
        {
            var result = await _mediator.Send(
                new HandleAiModelDeletedFeature.Command(message.DeletedModelName)
            );

            if (result.Success && result.AffectedConfigCount > 0)
            {
                OnPropertyChanged(nameof(Config));
                _dialogService.ShowToast(result.Message, "Warning");
            }
        }
    }
}

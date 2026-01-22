using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenAI.ObjectModels.RequestModels;
using PromptMasterv5.Core.Interfaces;
using PromptMasterv5.Core.Models;
using PromptMasterv5.Infrastructure.Services;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace PromptMasterv5.ViewModels
{
    public partial class QuickActionViewModel : ObservableObject
    {
        private readonly IAiService _aiService;
        private readonly ClipboardService _clipboardService;
        private readonly ISettingsService _settingsService;

        [ObservableProperty]
        private ObservableCollection<PromptItem> quickActions = new();

        [ObservableProperty]
        private string selectedText = "";

        [ObservableProperty]
        private ObservableCollection<ChatMessage> messages = new();

        [ObservableProperty]
        private string inputText = "";

        [ObservableProperty]
        private bool isExpanded = false;

        [ObservableProperty]
        private bool isProcessing = false;

        [ObservableProperty]
        private string currentResult = "";

        // Window reference for expanding animation
        public Action? OnExpandWindow { get; set; }
        public Action? OnCloseWindow { get; set; }

        public QuickActionViewModel(
            IAiService aiService,
            ClipboardService clipboardService,
            ISettingsService settingsService,
            string selectedText,
            ObservableCollection<PromptItem> quickActions)
        {
            _aiService = aiService;
            _clipboardService = clipboardService;
            _settingsService = settingsService;
            SelectedText = selectedText;
            QuickActions = quickActions;
        }

        [RelayCommand]
        private async Task ExecuteAction(PromptItem prompt)
        {
            if (IsProcessing) return;

            // Trigger window expansion
            IsExpanded = true;
            OnExpandWindow?.Invoke();

            // Assembly prompt with selected text
            string assembledPrompt = AssemblePrompt(prompt.Content ?? "", SelectedText);

            // Resolve model
            var (apiKey, baseUrl, model, systemPrompt) = ResolveModel(prompt);

            // Add user message
            Messages.Clear();
            Messages.Add(new ChatMessage("user", SelectedText));

            // Start AI streaming
            IsProcessing = true;
            CurrentResult = "";

            try
            {
                var apiMessages = new System.Collections.Generic.List<ChatMessage>();
                if (!string.IsNullOrEmpty(systemPrompt))
                {
                    apiMessages.Add(new ChatMessage("system", systemPrompt));
                }
                apiMessages.Add(new ChatMessage("user", assembledPrompt));

                await foreach (var chunk in _aiService.ChatStreamAsync(apiMessages, apiKey, baseUrl, model))
                {
                    CurrentResult += chunk;
                }

                Messages.Add(new ChatMessage("assistant", CurrentResult));
            }
            catch (Exception ex)
            {
                CurrentResult = $"[错误] {ex.Message}";
                Messages.Add(new ChatMessage("assistant", CurrentResult));
                LoggerService.Instance.LogError($"快捷指令执行失败: {ex.Message}", "QuickActionViewModel.ExecuteAction");
            }
            finally
            {
                IsProcessing = false;
            }
        }

        [RelayCommand]
        private async Task SendMessage()
        {
            if (string.IsNullOrWhiteSpace(InputText) || IsProcessing) return;

            string userInput = InputText.Trim();
            InputText = "";

            Messages.Add(new ChatMessage("user", userInput));

            IsProcessing = true;
            CurrentResult = "";

            try
            {
                // Get last used model from first execution
                var config = _settingsService.Config;
                string apiKey = config.AiApiKey;
                string baseUrl = config.AiBaseUrl;
                string model = config.AiModel;

                if (!string.IsNullOrEmpty(config.ActiveModelId))
                {
                    var activeModel = config.SavedModels.FirstOrDefault(m => m.Id == config.ActiveModelId);
                    if (activeModel != null)
                    {
                        apiKey = activeModel.ApiKey;
                        baseUrl = activeModel.BaseUrl;
                        model = activeModel.ModelName;
                    }
                }

                var apiMessages = Messages.ToList();

                await foreach (var chunk in _aiService.ChatStreamAsync(apiMessages, apiKey, baseUrl, model))
                {
                    CurrentResult += chunk;
                }

                Messages.Add(new ChatMessage("assistant", CurrentResult));
            }
            catch (Exception ex)
            {
                CurrentResult = $"[错误] {ex.Message}";
                Messages.Add(new ChatMessage("assistant", CurrentResult));
            }
            finally
            {
                IsProcessing = false;
            }
        }

        [RelayCommand]
        private void CopyResult()
        {
            if (!string.IsNullOrWhiteSpace(CurrentResult))
            {
                _clipboardService.SetClipboard(CurrentResult);
                OnCloseWindow?.Invoke();
            }
        }

        [RelayCommand]
        private void Replace()
        {
            if (!string.IsNullOrWhiteSpace(CurrentResult))
            {
                _clipboardService.SetClipboard(CurrentResult);
                OnCloseWindow?.Invoke();
                
                // Paste to active window
                Task.Delay(50).ContinueWith(_ => _clipboardService.PasteToActiveWindow());
            }
        }

        [RelayCommand]
        private async Task Retry()
        {
            if (Messages.Count >= 2)
            {
                // Remove last AI response
                Messages.RemoveAt(Messages.Count - 1);

                // Re-execute with same prompt
                var lastUserMessage = Messages.LastOrDefault(m => m.Role == "user");
                if (lastUserMessage != null)
                {
                    await ExecuteAction(QuickActions.First());
                }
            }
        }

        private string AssemblePrompt(string promptContent, string selectedText)
        {
            if (promptContent.Contains("{text}"))
            {
                return promptContent.Replace("{text}", selectedText);
            }
            else
            {
                return $"{promptContent}\n\n{selectedText}";
            }
        }

        private (string apiKey, string baseUrl, string model, string systemPrompt) ResolveModel(PromptItem prompt)
        {
            var config = _settingsService.Config;

            // Default values
            string apiKey = config.AiApiKey;
            string baseUrl = config.AiBaseUrl;
            string model = config.AiModel;
            string systemPrompt = "";

            // Check if prompt has bound model
            if (!string.IsNullOrEmpty(prompt.BoundModelId))
            {
                var boundModel = config.SavedModels.FirstOrDefault(m => m.Id == prompt.BoundModelId);
                if (boundModel != null)
                {
                    apiKey = boundModel.ApiKey;
                    baseUrl = boundModel.BaseUrl;
                    model = boundModel.ModelName;
                }
            }
            // Otherwise use active model
            else if (!string.IsNullOrEmpty(config.ActiveModelId))
            {
                var activeModel = config.SavedModels.FirstOrDefault(m => m.Id == config.ActiveModelId);
                if (activeModel != null)
                {
                    apiKey = activeModel.ApiKey;
                    baseUrl = activeModel.BaseUrl;
                    model = activeModel.ModelName;
                }
            }

            return (apiKey, baseUrl, model, systemPrompt);
        }
    }
}

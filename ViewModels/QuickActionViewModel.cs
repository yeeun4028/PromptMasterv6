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

        [ObservableProperty]
        private bool isRightToolbarVisible = false;

        [ObservableProperty]
        private bool isLargeMode = false;

        [ObservableProperty]
        private int currentLineCount = 0;

        [ObservableProperty]
        private bool isWindowLocked = false;

        // Store model configuration from first execution to maintain consistency
        private string? _lastUsedApiKey;
        private string? _lastUsedBaseUrl;
        private string? _lastUsedModel;

        // Window reference for expanding animation
        public Action? OnExpandWindow { get; set; }
        public Action? OnCloseWindow { get; set; }
        public Action? OnToggleLargeMode { get; set; }
        public Action<string>? OnSaveAndOpenMarkdown { get; set; }

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

            // Store for subsequent messages in this conversation
            _lastUsedApiKey = apiKey;
            _lastUsedBaseUrl = baseUrl;
            _lastUsedModel = model;

            // Add user message
            Messages.Clear();
            Messages.Add(new ChatMessage("user", SelectedText));

            // Start AI streaming
            IsProcessing = true;
            CurrentResult = "";
            CurrentLineCount = 0;
            _longTextHandled = false; // Reset for new action

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
                    CurrentLineCount = CountLines(CurrentResult);
                    
                    // Check for long text threshold
                    CheckLongTextThreshold();
                }

                Messages.Add(new ChatMessage("assistant", CurrentResult));
                
                // Show right toolbar buttons after first response
                IsRightToolbarVisible = true;
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
            CurrentLineCount = 0;
            _longTextHandled = false; // Reset for new message

            try
            {
                // Use the same model as the first execution to maintain conversation context
                string apiKey = _lastUsedApiKey ?? _settingsService.Config.AiApiKey;
                string baseUrl = _lastUsedBaseUrl ?? _settingsService.Config.AiBaseUrl;
                string model = _lastUsedModel ?? _settingsService.Config.AiModel;

                LoggerService.Instance.LogInfo($"Continuing conversation with model: {model}", "QuickActionViewModel.SendMessage");

                var apiMessages = Messages.ToList();

                await foreach (var chunk in _aiService.ChatStreamAsync(apiMessages, apiKey, baseUrl, model))
                {
                    CurrentResult += chunk;
                    CurrentLineCount = CountLines(CurrentResult);
                    
                    // Check for long text threshold
                    CheckLongTextThreshold();
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

            AiModelConfig? selectedModel = null;

            // Priority 1: Check if prompt has bound model
            if (!string.IsNullOrEmpty(prompt.BoundModelId))
            {
                selectedModel = config.SavedModels.FirstOrDefault(m => m.Id == prompt.BoundModelId);
                if (selectedModel != null)
                {
                    LoggerService.Instance.LogInfo($"Using bound model for prompt '{prompt.Title}': {selectedModel.ModelName}", "QuickActionViewModel.ResolveModel");
                }
                else
                {
                    LoggerService.Instance.LogWarning($"Bound model ID '{prompt.BoundModelId}' not found for prompt '{prompt.Title}', falling back to active model", "QuickActionViewModel.ResolveModel");
                }
            }

            // Priority 2: Fallback to active model if no bound model or bound model not found
            if (selectedModel == null && !string.IsNullOrEmpty(config.ActiveModelId))
            {
                selectedModel = config.SavedModels.FirstOrDefault(m => m.Id == config.ActiveModelId);
                if (selectedModel != null)
                {
                    LoggerService.Instance.LogInfo($"Using active model: {selectedModel.ModelName}", "QuickActionViewModel.ResolveModel");
                }
            }

            // Apply selected model if found
            if (selectedModel != null)
            {
                apiKey = selectedModel.ApiKey;
                baseUrl = selectedModel.BaseUrl;
                model = selectedModel.ModelName;
            }
            else
            {
                LoggerService.Instance.LogInfo($"Using default config values: {model}", "QuickActionViewModel.ResolveModel");
            }

            return (apiKey, baseUrl, model, systemPrompt);
        }

        [RelayCommand]
        private void ToggleWindowSize()
        {
            IsLargeMode = !IsLargeMode;
            OnToggleLargeMode?.Invoke();
        }

        [RelayCommand]
        private void CloseWindow()
        {
            OnCloseWindow?.Invoke();
        }

        private int CountLines(string text)
        {
            if (string.IsNullOrEmpty(text))
                return 0;

            return text.Split('\n').Length;
        }

        private bool _longTextHandled = false;

        private void CheckLongTextThreshold()
        {
            if (_longTextHandled) return;

            var config = _settingsService.Config;
            if (CurrentLineCount >= config.QuickActionLineThreshold)
            {
                _longTextHandled = true;

                if (config.QuickActionLongTextMode == Core.Models.QuickActionLongTextMode.ExternalEditor)
                {
                    // Save to markdown and open externally
                    string content = CurrentResult;
                    SaveAndOpenMarkdownFile(content);
                    
                    // Lock window to prevent auto-close
                    IsWindowLocked = true;
                    
                    LoggerService.Instance.LogInfo($"Long text detected ({CurrentLineCount} lines), saved to external editor", "QuickActionViewModel");
                }
                else
                {
                    // Expand window to large mode
                    IsLargeMode = true;
                    OnToggleLargeMode?.Invoke();
                    
                    LoggerService.Instance.LogInfo($"Long text detected ({CurrentLineCount} lines), expanded to large mode", "QuickActionViewModel");
                }
            }
        }

        private void SaveAndOpenMarkdownFile(string content)
        {
            try
            {
                // Create archive directory
                string appDir = AppDomain.CurrentDomain.BaseDirectory;
                string archiveDir = System.IO.Path.Combine(appDir, "QuickAction_Archive");
                
                if (!System.IO.Directory.Exists(archiveDir))
                {
                    System.IO.Directory.CreateDirectory(archiveDir);
                }

                // Generate filename: yyyyMMdd_HHmmss_first10chars.md
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string preview = content.Length > 10 ? content.Substring(0, 10) : content;
                preview = SanitizeFileName(preview);
                
                string fileName = $"{timestamp}_{preview}.md";
                string filePath = System.IO.Path.Combine(archiveDir, fileName);

                // Write file
                System.IO.File.WriteAllText(filePath, content, System.Text.Encoding.UTF8);

                // Open with system default editor
                var processInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = filePath,
                    UseShellExecute = true
                };
                System.Diagnostics.Process.Start(processInfo);

                LoggerService.Instance.LogInfo($"Saved and opened markdown file: {fileName}", "QuickActionViewModel");
            }
            catch (Exception ex)
            {
                LoggerService.Instance.LogError($"Failed to save/open markdown file: {ex.Message}", "QuickActionViewModel");
            }
        }

        private string SanitizeFileName(string fileName)
        {
            // Replace illegal characters with underscore
            char[] illegalChars = new[] { '<', '>', ':', '"', '/', '\\', '|', '?', '*', '\r', '\n', '\t' };
            
            foreach (char c in illegalChars)
            {
                fileName = fileName.Replace(c, '_');
            }

            return fileName.Trim();
        }
    }
}

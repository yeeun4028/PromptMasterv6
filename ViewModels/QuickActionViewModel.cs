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
        private PromptItem? currentPrompt;

        [ObservableProperty]
        private int currentLineCount = 0;

        [ObservableProperty]
        private bool isWindowLocked = false;

        // Model Selection
        [ObservableProperty]
        private ObservableCollection<AiModelConfig> availableModels = new();

        [ObservableProperty]
        private AiModelConfig? selectedOverrideModel;

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
            
            // Load available models
            LoadAvailableModels();
        }

        [RelayCommand]
        private async Task ExecuteAction(PromptItem prompt)
        {
            if (IsProcessing) return;

            // Trigger window expansion
            IsExpanded = true;
            OnExpandWindow?.Invoke();

            // Save current prompt for retry
            CurrentPrompt = prompt;

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

            await ContinueChat();
        }

        private async Task ContinueChat()
        {
            IsProcessing = true;
            CurrentResult = "";
            CurrentLineCount = 0;
            _longTextHandled = false; // Reset for new message

            try
            {
                // Resolve model priority: 
                // 1. Override Model (if set)
                // 2. Last Used Model (continuation)
                // 3. Default Config
                
                string apiKey = _settingsService.Config.AiApiKey;
                string baseUrl = _settingsService.Config.AiBaseUrl;
                string model = _settingsService.Config.AiModel;

                // Check Override
                if (SelectedOverrideModel != null && SelectedOverrideModel.Id != Guid.Empty.ToString())
                {
                    apiKey = SelectedOverrideModel.ApiKey;
                    baseUrl = SelectedOverrideModel.BaseUrl;
                    model = SelectedOverrideModel.ModelName;
                    LoggerService.Instance.LogInfo($"Continuing chat with override model: {model}", "QuickActionViewModel.ContinueChat");
                }
                else if (_lastUsedModel != null)
                {
                    // Fallback to last used if no override
                    apiKey = _lastUsedApiKey ?? apiKey;
                    baseUrl = _lastUsedBaseUrl ?? baseUrl;
                    model = _lastUsedModel ?? model;
                    LoggerService.Instance.LogInfo($"Continuing chat with last used model: {model}", "QuickActionViewModel.ContinueChat");
                }

                // Update last used for future turns
                _lastUsedApiKey = apiKey;
                _lastUsedBaseUrl = baseUrl;
                _lastUsedModel = model;

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
            if (Messages.Count < 2) return;

            // Remove last AI response
            Messages.RemoveAt(Messages.Count - 1);

            // Determine if we are retrying the INITIAL prompt (Single Turn) or a specific Chat Message (Multi Turn)
            // Logic: If we only have 1 message left (the User's initial Selected Text), we assume it's a Prompt Retry.
            if (Messages.Count == 1)
            {
                // Single Turn Retry: Re-run ExecuteAction to ensure System Prompt & Template are applied
                if (CurrentPrompt != null)
                {
                    await ExecuteAction(CurrentPrompt);
                }
                else if (QuickActions.Any())
                {
                    await ExecuteAction(QuickActions.First()); // Fallback
                }
            }
            else
            {
                // Multi Turn Retry: Continue the chat from the last user message
                // This preserves the conversation history
                await ContinueChat();
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

            // Use GetEffectiveModel which implements 3-tier priority
            var selectedModel = GetEffectiveModel(prompt);

            // Apply selected model if found
            if (selectedModel != null)
            {
                apiKey = selectedModel.ApiKey;
                baseUrl = selectedModel.BaseUrl;
                model = selectedModel.ModelName;
            }
            else
            {
                LoggerService.Instance.LogInfo($"No model found, using default config values: {model}", "QuickActionViewModel.ResolveModel");
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

        [RelayCommand]
        private void SelectModel(AiModelConfig? model)
        {
            SelectedOverrideModel = model;
            LoggerService.Instance.LogInfo($"Model override changed to: {model?.ModelName ?? "默认"}", "QuickActionViewModel");
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
            
            // Only check for internal expansion mode (external editor is now manual via button)
            if (CurrentLineCount >= config.QuickActionLineThreshold && config.QuickActionLongTextMode == Core.Models.QuickActionLongTextMode.InternalExpand)
            {
                _longTextHandled = true;
                
                // Expand window to large mode
                IsLargeMode = true;
                OnToggleLargeMode?.Invoke();
                
                LoggerService.Instance.LogInfo($"Long text detected ({CurrentLineCount} lines), expanded to large mode", "QuickActionViewModel");
            }
        }

        [RelayCommand]
        private void OpenInExternalEditor()
        {
            if (string.IsNullOrEmpty(CurrentResult))
            {
                LoggerService.Instance.LogWarning("No content to save", "QuickActionViewModel.OpenInExternalEditor");
                return;
            }

            // Save current result to markdown file and open
            SaveAndOpenMarkdownFile(CurrentResult);
            
            // Lock window to prevent auto-close
            IsWindowLocked = true;
            
            LoggerService.Instance.LogInfo("Manually opened result in external editor", "QuickActionViewModel");
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
        
        private void LoadAvailableModels()
        {
            AvailableModels.Clear();
            
            // Add "默认" option as first item (null value represents no override)
            var defaultOption = new AiModelConfig
            {
                Id = Guid.Empty.ToString(),
                ModelName = "清除选择",
                ApiKey = "",
                BaseUrl = ""
            };
            AvailableModels.Add(defaultOption);
            
            // Add all saved models
            foreach (var model in _settingsService.Config.SavedModels)
            {
                AvailableModels.Add(model);
            }
        }

        private AiModelConfig? GetEffectiveModel(PromptItem? prompt)
        {
            // Priority 1: Window-level override (if not "默认")
            if (SelectedOverrideModel != null && SelectedOverrideModel.Id != Guid.Empty.ToString())
            {
                LoggerService.Instance.LogInfo($"Using window override model: {SelectedOverrideModel.ModelName}", "QuickActionViewModel");
                return SelectedOverrideModel;
            }

            // Priority 2: Prompt-specific model
            if (prompt?.BoundModelId != null && prompt.BoundModelId != Guid.Empty.ToString())
            {
                var promptModel = _settingsService.Config.SavedModels
                    .FirstOrDefault(m => m.Id == prompt.BoundModelId);
                if (promptModel != null)
                {
                    LoggerService.Instance.LogInfo($"Using prompt-specific model: {promptModel.ModelName}", "QuickActionViewModel");
                    return promptModel;
                }
            }

            // Priority 3: Default model
            var defaultModel = _settingsService.Config.SavedModels
                .FirstOrDefault(m => m.Id == _settingsService.Config.ActiveModelId);
            LoggerService.Instance.LogInfo($"Using default model: {defaultModel?.ModelName ?? "None"}", "QuickActionViewModel");
            return defaultModel;
        }
    }
}

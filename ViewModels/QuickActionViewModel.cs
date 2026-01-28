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
        private ObservableCollection<ObservableChatMessage> messages = new();

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

            // 1. Trigger Visual State Changes immediately (UI Thread)
            IsExpanded = true;
            IsRightToolbarVisible = true; // Show tools immediately, hiding prompt buttons
            OnExpandWindow?.Invoke();

            // 2. Yield to UI thread to allow animations to start and layout to update
            // This removes the "perceived" lag by making the click instant
            await Task.Delay(50);

            // 3. Prepare Data (Off-thread to avoid any hitching during animation)
            // Capture necessary data to pass to background thread
            string rawPromptContent = prompt.Content ?? "";
            string currentSelectedText = SelectedText;
            
            // We need to keep CurrentPrompt update on UI thread later if it triggers UI binding changes, 
            // strictly speaking ObservableObject is thread-agnostic for the event but WPF handling is better on UI thread.
            // However, we want to save it. Let's do it here as it's quick assignment.
            CurrentPrompt = prompt;

            try
            {
                // Run heavy logic in background
                var processingTask = Task.Run(() =>
                {
                    // Assembly prompt with selected text
                    string assembled = AssemblePrompt(rawPromptContent, currentSelectedText);

                    // Resolve model
                    // Note: accessing _settingsService.Config is assumed thread-safe for reading string properties
                    var modelInfo = ResolveModel(prompt);

                    return (assembled, modelInfo);
                });

                var (assembledPrompt, (apiKey, baseUrl, model, systemPrompt)) = await processingTask;

                // 4. Update State with Results (Back on UI Thread)
                
                // Store for subsequent messages in this conversation
                _lastUsedApiKey = apiKey;
                _lastUsedBaseUrl = baseUrl;
                _lastUsedModel = model;

                // Add user message
                Messages.Clear();
                Messages.Add(new ObservableChatMessage("user", SelectedText));

                // Initialize assistant message for streaming
                var assistantMessage = new ObservableChatMessage("assistant", "");
                Messages.Add(assistantMessage);

                // Start AI streaming
                IsProcessing = true;
                CurrentResult = "";
                CurrentLineCount = 0;
                _longTextHandled = false; // Reset for new action

                var apiMessages = new System.Collections.Generic.List<ChatMessage>();
                if (!string.IsNullOrEmpty(systemPrompt))
                {
                    apiMessages.Add(new ChatMessage("system", systemPrompt));
                }
                apiMessages.Add(new ChatMessage("user", assembledPrompt));

                await foreach (var chunk in _aiService.ChatStreamAsync(apiMessages, apiKey, baseUrl, model))
                {
                    CurrentResult += chunk;
                    assistantMessage.Content = CurrentResult; // Real-time update
                    
                    // Optimized line counting (simple approximation for perf during streaming)
                    CurrentLineCount = CurrentResult.Count(c => c == '\n') + 1;
                    
                    // Check for long text threshold
                    CheckLongTextThreshold();
                }
            }
            catch (Exception ex)
            {
                CurrentResult = $"[错误] {ex.Message}";
                Messages.Add(new ObservableChatMessage("assistant", CurrentResult));
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

            Messages.Add(new ObservableChatMessage("user", userInput));

            await ContinueChat();
        }

        private async Task ContinueChat()
        {
            IsProcessing = true;
            CurrentResult = "";
            CurrentLineCount = 0;
            _longTextHandled = false; // Reset for new message

            // Initialize assistant message for streaming
            var assistantMessage = new ObservableChatMessage("assistant", "");
            Messages.Add(assistantMessage);

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

                var apiMessages = Messages.Select(m => new ChatMessage(m.Role, m.Content)).ToList();

                await foreach (var chunk in _aiService.ChatStreamAsync(apiMessages, apiKey, baseUrl, model))
                {
                    CurrentResult += chunk;
                    assistantMessage.Content = CurrentResult; // Real-time update

                    // Optimized line count
                    CurrentLineCount = CurrentResult.Count(c => c == '\n') + 1;
                    
                    // Check for long text threshold
                    CheckLongTextThreshold();
                }
            }
            catch (Exception ex)
            {
                CurrentResult = $"[错误] {ex.Message}";
                // If we already added the message, update it. If failed before adding, add it.
                if (assistantMessage != null)
                {
                    assistantMessage.Content += $"\n{CurrentResult}";
                }
                else
                {
                    Messages.Add(new ObservableChatMessage("assistant", CurrentResult));
                }
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

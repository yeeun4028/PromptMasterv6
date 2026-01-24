using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PromptMasterv5.Core.Interfaces;
using PromptMasterv5.Core.Models;
using PromptMasterv5.Infrastructure.Services;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using PromptMasterv5.Infrastructure.Helpers;
using HandyControl.Controls;
using HandyControl.Data;

namespace PromptMasterv5.ViewModels
{
    public partial class ExternalToolsViewModel : ObservableObject
    {
        private readonly ISettingsService _settingsService;
        private readonly IAiService _aiService;
        private readonly BaiduService _baiduService;
        private readonly GoogleService _googleService;

        public ObservableCollection<ApiProfile> OcrProfiles => 
            new(Config.ApiProfiles.Where(p => p.ServiceType == ServiceType.OCR));

        public ObservableCollection<ApiProfile> TranslateProfiles => 
            new(Config.ApiProfiles.Where(p => p.ServiceType == ServiceType.Translation));

        public void RefreshProfiles()
        {
            OnPropertyChanged(nameof(OcrProfiles));
            OnPropertyChanged(nameof(TranslateProfiles));
        }

        // Reference to MainViewModel to access Files/Folders if needed for AI Translation Prompt
        private MainViewModel? _mainViewModel;

        public void SetMainViewModel(MainViewModel mainViewModel)
        {
            _mainViewModel = mainViewModel;
        }

        public AppConfig Config => _settingsService.Config;
        public LocalSettings LocalConfig => _settingsService.LocalConfig;

        private readonly IDialogService _dialogService;
        private readonly IWindowManager _windowManager;

        public ExternalToolsViewModel(
            ISettingsService settingsService,
            IAiService aiService,
            BaiduService baiduService,
            GoogleService googleService,
            IDialogService dialogService,
            IWindowManager windowManager)
        {
            _settingsService = settingsService;
            _aiService = aiService;
            _baiduService = baiduService;
            _googleService = googleService;
            _dialogService = dialogService;
            _windowManager = windowManager;
            
            LoggerService.Instance.LogInfo("ExternalToolsViewModel initialized", "ExternalToolsViewModel.ctor");
            
            EnsureAiProfileExists();
        }

        private void EnsureAiProfileExists()
        {
            // Ensure there is at least one AI profile for selection
            if (!Config.ApiProfiles.Any(p => p.Provider == ApiProvider.AI && p.ServiceType == ServiceType.Translation))
            {
                Config.ApiProfiles.Add(new ApiProfile 
                { 
                    Name = "AI 智能翻译", 
                    Provider = ApiProvider.AI, 
                    ServiceType = ServiceType.Translation,
                    Id = Guid.NewGuid().ToString() // Ensure ID is set
                });
                _settingsService.SaveConfig();
                RefreshProfiles();
            }
        }

        [RelayCommand]
        private async Task TriggerOcr()
        {
            var enabledProfiles = OcrProfiles.Where(p => p.IsEnabled).ToList();
            if (enabledProfiles.Count == 0)
            {
                _dialogService.ShowAlert("请先在设置中勾选至少一个 OCR 服务商。", "未配置 OCR");
                return;
            }

            // Use WindowManager
            var capturedBytes = _windowManager.ShowCaptureWindow();
            if (capturedBytes == null) return;

            // Execute OCR Racing
            var result = await RaceOcrAsync(capturedBytes, enabledProfiles);

            if (!string.IsNullOrWhiteSpace(result))
            {
                if (result.StartsWith("OCR 错误") || result.StartsWith("错误"))
                {
                    _dialogService.ShowAlert(result, "OCR 失败");
                }
                else
                {
                    System.Windows.Clipboard.SetText(result);
                    Growl.SuccessGlobal("文字识别成功，已复制到剪贴板。");
                }
            }
        }

        [RelayCommand]
        private async Task TriggerTranslate()
        {
            var enabledOcrProfiles = OcrProfiles.Where(p => p.IsEnabled).ToList();
            var enabledTransProfiles = TranslateProfiles.Where(p => p.IsEnabled).ToList();

            if (enabledOcrProfiles.Count == 0)
            {
                _dialogService.ShowAlert("请先在设置中勾选至少一个 OCR 服务商。", "未配置 OCR");
                return;
            }
            if (enabledTransProfiles.Count == 0)
            {
                _dialogService.ShowAlert("请先在设置中勾选至少一个翻译服务商。", "未配置翻译");
                return;
            }

            var capturedBytes = _windowManager.ShowCaptureWindow();
            if (capturedBytes == null) return;

            // 1. OCR Racing
            var text = await RaceOcrAsync(capturedBytes, enabledOcrProfiles);
            
            if (string.IsNullOrWhiteSpace(text) || text.StartsWith("OCR 错误") || text.StartsWith("错误"))
            {
                _dialogService.ShowAlert(text ?? "无法识别文字", "文字识别失败");
                return;
            }

            // 2. Translation Racing
            var translated = await RaceTranslateAsync(text, enabledTransProfiles);

            if (Config.AutoCopyTranslationResult && !string.IsNullOrWhiteSpace(translated))
            {
                try { System.Windows.Clipboard.SetText(translated); } catch { }
            }

            // 3. Show Result
            _windowManager.ShowTranslationPopup(translated ?? "翻译失败");
        }

        [RelayCommand]
        private async Task TriggerSelectedTextTranslate()
        {
            var enabledTransProfiles = TranslateProfiles.Where(p => p.IsEnabled).ToList();
            if (enabledTransProfiles.Count == 0)
            {
                 // Check if it's just a misconfiguration or intended fallback
                 // But for selected text translate, we need at least one translator.
                 // Unless we fallback to screenshot mode which needs OCR.
            }

            try
            {
                // 1. Clear clipboard
                try { System.Windows.Clipboard.Clear(); } catch { }

                // 2. Simulate Ctrl+C via SendInput (Robust method)
                // Notify MainViewModel to ignore key events
                if (_mainViewModel != null) _mainViewModel.SetSimulatingKeys(true);
                
                try 
                {
                    InputHelper.SendCopyCommand();
                    
                    // 3. Loop check clipboard
                    string text = "";
                    for (int i = 0; i < 10; i++)
                    {
                        await Task.Delay(50);
                        try 
                        {
                            if (System.Windows.Clipboard.ContainsText())
                            {
                                text = System.Windows.Clipboard.GetText();
                                if (!string.IsNullOrWhiteSpace(text)) break;
                            }
                        }
                        catch { }
                    }

                    // 4. Secondary Attempt: WM_COPY (if Ctrl+C failed)
                    if (string.IsNullOrWhiteSpace(text))
                    {
                        if (InputHelper.TrySendWmCopy())
                        {
                             // Wait briefly for message to process
                             await Task.Delay(100);
                             try 
                             {
                                 if (System.Windows.Clipboard.ContainsText())
                                 {
                                     text = System.Windows.Clipboard.GetText();
                                 }
                             }
                             catch { }
                        }
                    }

                    if (string.IsNullOrWhiteSpace(text))
                    {
                        // 5. Auto-Screenshot Fallback (Perfect Solution)
                        // If text copy fails, seamlessly assume it's non-selectable text and switch to screenshot mode.
                        
                        Growl.InfoGlobal(new GrowlInfo
                        {
                            Message = "无法直接取词，已自动切换至截图模式...",
                            ShowDateTime = false,
                            WaitTime = 2
                        });

                        await Task.Delay(200);

                        if (TriggerTranslateCommand.CanExecute(null))
                        {
                            TriggerTranslateCommand.Execute(null);
                        }
                        return;
                    }

                    if (enabledTransProfiles.Count == 0)
                    {
                        _dialogService.ShowAlert("请先在设置中勾选至少一个翻译服务商。", "未配置翻译");
                        return;
                    }

                    var translated = await RaceTranslateAsync(text, enabledTransProfiles);

                    if (string.IsNullOrWhiteSpace(translated)) return;

                    if (Config.AutoCopyTranslationResult)
                    {
                        try { await System.Windows.Application.Current.Dispatcher.InvokeAsync(() => System.Windows.Clipboard.SetText(translated)); } catch { }
                    }

                    // 6. Show Result
                    _windowManager.ShowTranslationPopup(translated);
                }
                finally
                {
                    // Delay reset
                    await Task.Delay(200);
                    if (_mainViewModel != null) _mainViewModel.SetSimulatingKeys(false);
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowAlert($"划词翻译出错: {ex.Message}", "错误");
            }
        }

        private async Task<string> RaceOcrAsync(byte[] imageBytes, List<ApiProfile> profiles)
        {
            if (profiles == null || profiles.Count == 0) return "未启用 OCR 服务";

            // Create a task for each profile
            var tasks = profiles.Select(async profile =>
            {
                try
                {
                    return profile!.Provider switch
                    {
                        ApiProvider.Baidu => await _baiduService.OcrAsync(imageBytes, profile),
                        // Add other providers here when implemented
                        // ApiProvider.TencentCloud => await _tencentService.OcrAsync(...),
                        _ => ""
                    };
                }
                catch
                {
                    return ""; // Swallow individual errors
                }
            }).ToList();

            // Racing Logic: Return first non-empty result
            while (tasks.Count > 0)
            {
                var finishedTask = await Task.WhenAny(tasks);
                tasks.Remove(finishedTask);

                var result = await finishedTask;
                if (!string.IsNullOrWhiteSpace(result) && !result.StartsWith("OCR 错误") && !result.StartsWith("错误"))
                {
                    return result; // Winner!
                }
            }

            return "OCR 失败：所有服务均未能识别文字";
        }

        private async Task<string> RaceTranslateAsync(string text, List<ApiProfile> profiles)
        {
            if (string.IsNullOrWhiteSpace(text)) return "";
            if (profiles == null || profiles.Count == 0) return "未启用翻译服务";

            var tasks = profiles.Select(async profile =>
            {
                try
                {
                    return profile!.Provider switch
                    {
                        ApiProvider.Google => await _googleService.TranslateAsync(text, profile),
                        ApiProvider.Baidu => await _baiduService.TranslateAsync(text, profile, "auto", "zh"),
                        ApiProvider.AI => await TranslateWithAiAsync(text),
                        _ => ""
                    };
                }
                catch
                {
                    return "";
                }
            }).ToList();

            while (tasks.Count > 0)
            {
                var finishedTask = await Task.WhenAny(tasks);
                tasks.Remove(finishedTask);

                var result = await finishedTask;
                if (!string.IsNullOrWhiteSpace(result) && !result.StartsWith("翻译失败") && !result.StartsWith("错误"))
                {
                    return result; // Winner!
                }
            }

            return "翻译失败：所有服务均无响应";
        }

        private async Task<string> TranslateWithAiAsync(string text)
        {
            try
            {
                string systemPrompt = @"# 身份与目的

您是一位专业的翻译专家，接收的单词、短语、句子或文档作为输入，并尽最大努力将其尽可能准确、完美地翻译成**简体中文**。

请退后一步，深呼吸，并逐步思考如何实现以下步骤所定义的最佳结果。您有很大的自由度来确保这项工作顺利进行。您是有史以来最优秀的翻译专家。

## 输出部分

- 输入的原始格式必须保持不变。

- 您将逐句翻译，并保持原句的语气。

- 您不会操纵措辞以改变原意。

## 输出说明

- 不要输出警告或注释——只输出请求的翻译。

- 尽可能准确地翻译文档，保持原始文本的 1:1 副本，并将其翻译为**简体中文**。

- 不要更改格式，必须保持原样。

## 输入

输入：";
                
                if (!string.IsNullOrWhiteSpace(Config.AiTranslationPromptId) && _mainViewModel != null)
                {
                    var promptFile = _mainViewModel.Files.FirstOrDefault(f => f.Id == Config.AiTranslationPromptId);
                    if (promptFile != null && !string.IsNullOrWhiteSpace(promptFile.Content))
                    {
                        systemPrompt = promptFile.Content;
                    }
                }

                // Resolve Model Configuration
                string apiKey = Config.AiTranslateApiKey;
                string baseUrl = Config.AiTranslateBaseUrl;
                string modelId = Config.AiTranslateModel;

                // 1. Try Specific Translation Model
                if (!string.IsNullOrWhiteSpace(Config.TranslationModelId))
                {
                    var model = Config.SavedModels.FirstOrDefault(m => m.Id == Config.TranslationModelId);
                    if (model != null)
                    {
                        apiKey = model.ApiKey;
                        baseUrl = model.BaseUrl;
                        modelId = model.ModelName;
                    }
                }
                // 2. Fallback to Active Model (if manual keys are empty)
                else if (string.IsNullOrWhiteSpace(apiKey) && !string.IsNullOrWhiteSpace(Config.ActiveModelId))
                {
                    var model = Config.SavedModels.FirstOrDefault(m => m.Id == Config.ActiveModelId);
                    if (model != null)
                    {
                        apiKey = model.ApiKey;
                        baseUrl = model.BaseUrl;
                        modelId = model.ModelName;
                    }
                }

                if (string.IsNullOrWhiteSpace(apiKey)) return "AI 翻译失败：未配置有效的 API Key";

                return await _aiService.ChatAsync(text, apiKey, baseUrl, modelId, systemPrompt) 
                       ?? "AI 翻译失败：未返回结果";
            }
            catch (Exception ex)
            {
                return $"AI 翻译异常: {ex.Message}";
            }
        }
    }
}

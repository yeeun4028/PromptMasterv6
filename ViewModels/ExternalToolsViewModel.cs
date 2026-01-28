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
        private bool _isCapturing; // Prevent re-entrant screenshot calls
        private readonly ISettingsService _settingsService;
        private readonly IAiService _aiService;
        private readonly BaiduService _baiduService;
        private readonly TencentService _tencentService;
        private readonly GoogleService _googleService;
        private readonly IAudioService _audioService;

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
            TencentService tencentService,
            GoogleService googleService,
            IDialogService dialogService,
            IWindowManager windowManager,
            IAudioService audioService)
        {
            _settingsService = settingsService;
            _aiService = aiService;
            _baiduService = baiduService;
            _tencentService = tencentService;
            _googleService = googleService;
            _dialogService = dialogService;
            _windowManager = windowManager;
            _audioService = audioService;
            
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
                    Id = Guid.NewGuid().ToString() 
                });
            }

            // Ensure AI OCR profile exists
            if (!Config.ApiProfiles.Any(p => p.Provider == ApiProvider.AI && p.ServiceType == ServiceType.OCR))
            {
                Config.ApiProfiles.Add(new ApiProfile
                {
                    Name = "AI 智能 OCR",
                    Provider = ApiProvider.AI,
                    ServiceType = ServiceType.OCR,
                    Id = Guid.NewGuid().ToString()
                });
            }

            _settingsService.SaveConfig();
            RefreshProfiles();
        }

        [RelayCommand]
        private async Task TriggerOcr()
        {
            if (_isCapturing) return;

            try
            {
                _isCapturing = true;
                var enabledProfiles = OcrProfiles.Where(p => p.IsEnabled).ToList();
                if (enabledProfiles.Count == 0)
                {
                    _dialogService.ShowAlert("请先在设置中勾选至少一个 OCR 服务商。", "未配置 OCR");
                    return;
                }

                // Use WindowManager with callback for Processing State
                string? ocrResult = null;
                
                var capturedBytes = _windowManager.ShowCaptureWindow(async (byte[] bytes, System.Windows.Rect rect) => 
                {
                    // Execute OCR Racing inside the Processing State
                    ocrResult = await RaceOcrAsync(bytes, enabledProfiles);
                });

                if (capturedBytes == null) return;

                if (!string.IsNullOrWhiteSpace(ocrResult))
                {
                    if (ocrResult.StartsWith("OCR 错误") || ocrResult.StartsWith("错误") || ocrResult.StartsWith("OCR 失败"))
                    {
                        _dialogService.ShowAlert(ocrResult, "OCR 失败");
                    }
                    else
                    {
                        System.Windows.Clipboard.SetText(ocrResult);
                        // Growl.SuccessGlobal("文字识别成功，已复制到剪贴板。");
                        // Play Shutter Sound
                        await _audioService.PlayShutterSoundAsync();
                    }
                }
            }
            finally
            {
                _isCapturing = false;
            }
        }
    
        [RelayCommand]
        private async Task TriggerTranslate()
        {
            if (_isCapturing) return;

            try
            {
                _isCapturing = true;
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

                string? translatedResult = null;
                System.Windows.Rect? actionRect = null;

                var capturedBytes = _windowManager.ShowCaptureWindow(async (byte[] bytes, System.Windows.Rect rect) =>
                {
                    actionRect = rect;
                    // 1. OCR Racing
                    var text = await RaceOcrAsync(bytes, enabledOcrProfiles);
                    
                    if (string.IsNullOrWhiteSpace(text) || text.StartsWith("OCR 错误") || text.StartsWith("错误"))
                    {
                        // We can't show alert here easily if it blocks, but we can store error or just return.
                        // Let's store error message in result to show later
                        translatedResult = text ?? "无法识别文字";
                        return; // Stop translation
                    }

                    // 2. Translation Racing
                    translatedResult = await RaceTranslateAsync(text, enabledTransProfiles);
                });

                if (capturedBytes == null) return;

                // Handle Results
                if (string.IsNullOrWhiteSpace(translatedResult) || translatedResult.StartsWith("OCR 错误") || translatedResult.StartsWith("错误"))
                {
                     // If it was an error message from OCR step
                     if (translatedResult != null && (translatedResult.StartsWith("OCR 错误") || translatedResult.StartsWith("错误") || translatedResult == "无法识别文字"))
                     {
                         _dialogService.ShowAlert(translatedResult, "文字识别失败");
                     }
                     else
                     {
                         // Or translation failed but returned null/empty?
                         _windowManager.ShowTranslationPopup(translatedResult ?? "翻译失败");
                     }
                     return;
                }

                if (Config.AutoCopyTranslationResult && !string.IsNullOrWhiteSpace(translatedResult))
                {
                    try { System.Windows.Clipboard.SetText(translatedResult); } catch { }
                }

                // Play Shutter Sound
                await _audioService.PlayShutterSoundAsync();

                // 3. Show Result
                _windowManager.ShowTranslationPopup(translatedResult ?? "翻译失败", actionRect);
            }
            finally
            {
                _isCapturing = false;
            }
        }

        [RelayCommand]
        private async Task TriggerSelectedTextTranslate()
        {
            var enabledTransProfiles = TranslateProfiles.Where(p => p.IsEnabled).ToList();
            if (enabledTransProfiles.Count == 0)
            {
                 // Check if it's just a misconfiguration or intended fallback
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
            if (imageBytes == null || imageBytes.Length == 0) return "图片数据为空";
            
            var tasks = new List<Task<string>>();

            // 1. Standard Providers Tasks
            if (profiles != null && profiles.Count > 0)
            {
                var standardProfiles = profiles.Where(p => p.Provider != ApiProvider.AI).ToList();
                tasks.AddRange(standardProfiles.Select(async profile =>
                {
                    try
                    {
                        return profile!.Provider switch
                        {

                            ApiProvider.Baidu => await _baiduService.OcrAsync(imageBytes, profile).ConfigureAwait(false),
                            ApiProvider.Tencent => await _tencentService.OcrAsync(imageBytes, profile).ConfigureAwait(false),
                            // Add other providers here when implemented
                            _ => ""
                        };
                    }
                    catch { return ""; }
                }));
            }

            // 2. AI Models Tasks for OCR
            // Check if "AI 智能 OCR" is enabled in the *Main* profiles list passed to this method
            bool isAiOcrEnabled = profiles != null && profiles.Any(p => p.Provider == ApiProvider.AI && p.ServiceType == ServiceType.OCR);
            
            if (isAiOcrEnabled)
            {
                var enabledAiModels = Config.SavedModels.Where(m => m.IsEnableForOcr).ToList();
                if (enabledAiModels.Any())
                {
                    tasks.AddRange(enabledAiModels.Select(async model =>
                    {
                        try
                        {
                            return await OcrWithAiModelAsync(imageBytes, model).ConfigureAwait(false);
                        }
                        catch { return ""; }
                    }));
                }
            }

            if (tasks.Count == 0) return "未启用任何 OCR 服务 (Standard or AI), 或者开启了 AI OCR 但未选择模型";

            // 3. Racing Logic
            while (tasks.Count > 0)
            {
                var finishedTask = await Task.WhenAny(tasks).ConfigureAwait(false);
                tasks.Remove(finishedTask);

                try
                {
                    var result = await finishedTask.ConfigureAwait(false);
                    if (!string.IsNullOrWhiteSpace(result) && !result.StartsWith("OCR 错误") && !result.StartsWith("错误"))
                    {
                        return result; // Winner!
                    }
                }
                catch { /* Ignore individual task failure */ }
            }

            return "OCR 失败：所有服务均未能识别文字";
        }

        private async Task<string> OcrWithAiModelAsync(byte[] imageBytes, AiModelConfig model)
        {
            try
            {
                string ocrSystemPrompt = @"# 角色任务
您是一个纯粹的 OCR 在线引擎。您的唯一任务是识别图像中的所有可见文字。

# 输出要求
1. 直接输出识别到的文本内容。
2. 保持原始文本的换行格式。
3. 不要包含任何开场白（如“图片的文字是...”）。
4. 不要包含任何解释、注脚或Markdown代码块标记。
5. 如果图片中没有文字，输出“无文字”。";

                return await _aiService.ChatWithImageAsync(imageBytes, model.ApiKey, model.BaseUrl, model.ModelName, ocrSystemPrompt).ConfigureAwait(false);
            }
            catch (Exception)
            {
                // Optionally log
                return "";
            }
        }

        private async Task<string> RaceTranslateAsync(string text, List<ApiProfile> profiles)
        {
            if (string.IsNullOrWhiteSpace(text)) return "";
            
            var tasks = new List<Task<string>>();

            // 1. Standard Providers Tasks
            if (profiles != null && profiles.Count > 0)
            {
                tasks.AddRange(profiles.Select(async profile =>
                {
                    try
                    {
                        return profile!.Provider switch
                        {
                            ApiProvider.Google => await _googleService.TranslateAsync(text, profile).ConfigureAwait(false),
                            ApiProvider.Baidu => await _baiduService.TranslateAsync(text, profile, "auto", "zh").ConfigureAwait(false),
                            ApiProvider.Tencent => await _tencentService.TranslateAsync(text, profile, "auto", "zh").ConfigureAwait(false),
                            // ApiProvider.AI is now handled separately below
                            _ => ""
                        };
                    }
                    catch { return ""; }
                }));
            }

            // 2. AI Models Tasks
            var enabledAiModels = Config.SavedModels.Where(m => m.IsEnableForTranslation).ToList();
            if (enabledAiModels.Any())
            {
                string systemPrompt = GetAiTranslationSystemPrompt();
                
                tasks.AddRange(enabledAiModels.Select(async model =>
                {
                    try
                    {
                        return await TranslateWithAiModelAsync(text, model, systemPrompt).ConfigureAwait(false);
                    }
                    catch { return ""; }
                }));
            }

            if (tasks.Count == 0) return "未启用任何翻译服务 (Standard or AI)";

            // 3. Racing Logic
            while (tasks.Count > 0)
            {
                var finishedTask = await Task.WhenAny(tasks).ConfigureAwait(false);
                tasks.Remove(finishedTask);

                try
                {
                    var result = await finishedTask.ConfigureAwait(false);
                    if (!string.IsNullOrWhiteSpace(result) && !result.StartsWith("翻译失败") && !result.StartsWith("错误") && !result.StartsWith("AI 翻译失败"))
                    {
                        return result; // Winner!
                    }
                }
                catch { /* Ignore individual task failure */ }
            }

            return "翻译失败：所有服务均无响应";
        }

        private string GetAiTranslationSystemPrompt()
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
            return systemPrompt;
        }

        private async Task<string> TranslateWithAiModelAsync(string text, AiModelConfig model, string systemPrompt)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(model.ApiKey)) return "";

                return await _aiService.ChatAsync(text, model.ApiKey, model.BaseUrl, model.ModelName, systemPrompt).ConfigureAwait(false) 
                       ?? "";
            }
            catch (Exception)
            {
                // Optionally log error
                return "";
            }
        }
    }
}

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

namespace PromptMasterv5.ViewModels
{
    public partial class ExternalToolsViewModel : ObservableObject
    {
        private readonly ISettingsService _settingsService;
        private readonly IAiService _aiService;
        private readonly BaiduService _baiduService;
        private readonly GoogleService _googleService;

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
        }

        [RelayCommand]

        private async Task TriggerOcr()
        {
            if (!TryGetOcrProfile(out var ocrProfile)) return;

            // Use WindowManager
            var capturedBytes = _windowManager.ShowCaptureWindow();
            if (capturedBytes == null) return;

            // Execute OCR
            var result = await _baiduService.OcrAsync(capturedBytes, ocrProfile!); // Suppress warning, TryGet ensures not null if true

            if (!string.IsNullOrWhiteSpace(result))
            {
                if (result.StartsWith("OCR 错误") || result.StartsWith("错误"))
                {
                    _dialogService.ShowAlert(result, "OCR 失败");
                }
                else
                {
                    System.Windows.Clipboard.SetText(result);
                }
            }
        }

        [RelayCommand]
        private async Task TriggerTranslate()
        {
            // OcrProfile is needed to extract text from image first
            if (!TryGetOcrProfile(out var ocrProfile)) return;

            var capturedBytes = _windowManager.ShowCaptureWindow();
            if (capturedBytes == null) return;

            // 1. OCR to extract text
            var text = await _baiduService.OcrAsync(capturedBytes, ocrProfile!); // Suppress warning
            if (string.IsNullOrWhiteSpace(text) || text.StartsWith("OCR 错误") || text.StartsWith("错误"))
            {
                _dialogService.ShowAlert(text, "文字识别失败");
                return;
            }

            // 2. Translate Logic
            var translated = await PerformTranslation(text);

            if (Config.AutoCopyTranslationResult && !string.IsNullOrWhiteSpace(translated))
            {
                try { System.Windows.Clipboard.SetText(translated); } catch { }
            }

            // 3. Show Result
            _windowManager.ShowTranslationPopup(translated);
        }

        [RelayCommand]
        private async Task TriggerSelectedTextTranslate()
        {
            try
            {
                // 1. Clear clipboard
                try { System.Windows.Clipboard.Clear(); } catch { }

                // 2. Simulate Ctrl+C
                // Notify MainViewModel to ignore key events
                if (_mainViewModel != null) _mainViewModel.SetSimulatingKeys(true);
                
                try 
                {
                    NativeMethods.keybd_event(NativeMethods.VK_CONTROL, 0, 0, 0);
                    NativeMethods.keybd_event(NativeMethods.VK_C, 0, 0, 0);
                    NativeMethods.keybd_event(NativeMethods.VK_C, 0, NativeMethods.KEYEVENTF_KEYUP, 0);
                    NativeMethods.keybd_event(NativeMethods.VK_CONTROL, 0, NativeMethods.KEYEVENTF_KEYUP, 0);
                    
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

                if (string.IsNullOrWhiteSpace(text))
                {
                    return;
                }

                var translated = await PerformTranslation(text);

                if (string.IsNullOrWhiteSpace(translated)) return;

                if (Config.AutoCopyTranslationResult)
                {
                    try { await System.Windows.Application.Current.Dispatcher.InvokeAsync(() => System.Windows.Clipboard.SetText(translated)); } catch { }
                }

                // 4. Show Result
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

    private async Task<string> PerformTranslation(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";

        // 1. AI Translation
        if (Config.EnableAiTranslation)
        {
            return await TranslateWithAiAsync(text);
        }

        // 2. Google
        if (Config.EnableGoogle)
        {
            var profile = Config.ApiProfiles.FirstOrDefault(p => p.Provider == ApiProvider.Google && p.ServiceType == ServiceType.Translation);
            if (profile != null)
            {
                return await _googleService.TranslateAsync(text, profile);
            }
            return "Google 翻译已开启，但未找到有效的 API 配置。";
        }

        // 3. Youdao (Not implemented in snippet)

        // 4. Baidu (Fallback or if enabled)
        // Need to get TranslateProfile first
            if (TryGetTranslateProfile(out var transProfile))
        {
                return await _baiduService.TranslateAsync(text, transProfile!, "auto", "zh");
        }
        else
        {
            // If Baidu is enabled but no profile found/selected:
            if (Config.EnableBaidu)
            {
                return "Baidu 翻译已启用但未选择有效的配置 (Profile)。请检查设置。";
            }
        }
        
        // Fallback: If no dedicated translation enabled, maybe try AI if configured
            // Check if AI Translation is possible (either via new Model ID or old manual keys)
            bool hasAiConfig = !string.IsNullOrWhiteSpace(Config.TranslationModelId) || 
                               !string.IsNullOrWhiteSpace(Config.ActiveModelId) ||
                               !string.IsNullOrWhiteSpace(Config.AiTranslateApiKey);

            if (hasAiConfig)
            {
                 return await TranslateWithAiAsync(text);
            }

            return "请先在设置中配置并开启翻译服务 (AI / Google / 百度)";
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

        private bool TryGetOcrProfile(out ApiProfile? profile)
        {
            profile = null;
            if (!Config.EnableBaidu)
            {
                // If OCR is not explicitly enabled via Baidu, check if others could do it? Currently only Baidu OCR supported in code?
                // The original code checked specific profiles.
                _dialogService.ShowAlert("请先启用百度 OCR 功能", "配置缺失");
                return false;
            }

            // Logic to get profile from Config using OcrProfileId
            if (!string.IsNullOrWhiteSpace(Config.OcrProfileId))
            {
                profile = Config.ApiProfiles.FirstOrDefault(p => p.Id == Config.OcrProfileId);
            }
            
            // Fallback to active profile if specific one not set
            if (profile == null && !string.IsNullOrWhiteSpace(Config.ActiveApiProfileId))
            {
                profile = Config.ApiProfiles.FirstOrDefault(p => p.Id == Config.ActiveApiProfileId);
            }

            if (profile == null)
            {
                _dialogService.ShowAlert("未找到有效的 API 配置，请在设置中添加并选中一个配置。", "配置缺失");
                return false;
            }
            return true;
        }

        private bool TryGetTranslateProfile(out ApiProfile? profile)
        {
            profile = null;
            if (!string.IsNullOrWhiteSpace(Config.TranslateProfileId))
            {
                profile = Config.ApiProfiles.FirstOrDefault(p => p.Id == Config.TranslateProfileId);
            }
            if (profile == null && !string.IsNullOrWhiteSpace(Config.ActiveApiProfileId))
            {
                profile = Config.ApiProfiles.FirstOrDefault(p => p.Id == Config.ActiveApiProfileId);
            }
            return profile != null;
        }
    }
}

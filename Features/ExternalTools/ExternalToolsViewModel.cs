using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using PromptMasterv6.Core.Interfaces;
using PromptMasterv6.Core.Models;
using PromptMasterv6.Infrastructure.Services;
using PromptMasterv6.Features.Shared.Messages;
using PromptMasterv6.Features.Main.Messages;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using PromptMasterv6.Infrastructure.Helpers;
using HandyControl.Controls;
using HandyControl.Data;

namespace PromptMasterv6.Features.ExternalTools
{
    public partial class ExternalToolsViewModel : ObservableObject
    {
        private bool _isCapturing;
        private readonly ISettingsService _settingsService;
        private readonly IAiService _aiService;
        private readonly IBaiduService _baiduService;
        private readonly ITencentService _tencentService;
        private readonly IGoogleService _googleService;

        private List<ApiProfile>? _cachedOcrProfiles;
        private List<ApiProfile>? _cachedTranslateProfiles;
        private int _lastConfigVersion = -1;

        private List<ApiProfile> GetEnabledOcrProfiles()
        {
            var currentCount = Config.ApiProfiles.Count;
            if (_cachedOcrProfiles == null || _lastConfigVersion != currentCount)
            {
                _cachedOcrProfiles = Config.ApiProfiles.Where(p => p.ServiceType == ServiceType.OCR && p.IsEnabled).ToList();
                _cachedTranslateProfiles = null;
                _lastConfigVersion = currentCount;
            }
            return _cachedOcrProfiles!;
        }

        private List<ApiProfile> GetEnabledTranslateProfiles()
        {
            var currentCount = Config.ApiProfiles.Count;
            if (_cachedTranslateProfiles == null || _lastConfigVersion != currentCount)
            {
                _cachedTranslateProfiles = Config.ApiProfiles.Where(p => p.ServiceType == ServiceType.Translation && p.IsEnabled).ToList();
                _cachedOcrProfiles = null;
                _lastConfigVersion = currentCount;
            }
            return _cachedTranslateProfiles!;
        }

        public ObservableCollection<ApiProfile> OcrProfiles => 
            new(Config.ApiProfiles.Where(p => p.ServiceType == ServiceType.OCR));

        public ObservableCollection<ApiProfile> TranslateProfiles => 
            new(Config.ApiProfiles.Where(p => p.ServiceType == ServiceType.Translation));

        public void RefreshProfiles()
        {
            _cachedOcrProfiles = null;
            _cachedTranslateProfiles = null;
            _lastConfigVersion = -1;
            
            OnPropertyChanged(nameof(OcrProfiles));
            OnPropertyChanged(nameof(TranslateProfiles));
        }

        public AppConfig Config => _settingsService.Config;
        public LocalSettings LocalConfig => _settingsService.LocalConfig;

        private readonly IDialogService _dialogService;
        private readonly IWindowManager _windowManager;

        public ExternalToolsViewModel(
            ISettingsService settingsService,
            IAiService aiService,
            IBaiduService baiduService,
            ITencentService tencentService,
            IGoogleService googleService,
            IDialogService dialogService,
            IWindowManager windowManager,
            ClipboardService clipboardService)
        {
            _settingsService = settingsService;
            _aiService = aiService;
            _baiduService = baiduService;
            _tencentService = tencentService;
            _googleService = googleService;
            _dialogService = dialogService;
            _windowManager = windowManager;
            _windowManager = windowManager;
            _clipboardService = clipboardService;
            
            LoggerService.Instance.LogInfo("ExternalToolsViewModel initialized", "ExternalToolsViewModel.ctor");
            
            EnsureAiProfileExists();
        }

        private readonly ClipboardService _clipboardService;
        
        private void EnsureAiProfileExists()
        {
            bool added = false;

            if (!Config.ApiProfiles.Any(p => p.Provider == ApiProvider.AI && p.ServiceType == ServiceType.Translation))
            {
                Config.ApiProfiles.Add(new ApiProfile 
                { 
                    Name = "AI 智能翻译", 
                    Provider = ApiProvider.AI, 
                    ServiceType = ServiceType.Translation,
                    Id = Guid.NewGuid().ToString() 
                });
                added = true;
            }

            if (!Config.ApiProfiles.Any(p => p.Provider == ApiProvider.AI && p.ServiceType == ServiceType.OCR))
            {
                Config.ApiProfiles.Add(new ApiProfile
                {
                    Name = "AI 智能 OCR",
                    Provider = ApiProvider.AI,
                    ServiceType = ServiceType.OCR,
                    Id = Guid.NewGuid().ToString()
                });
                added = true;
            }

            if (added) _settingsService.SaveConfig();
            RefreshProfiles();
        }

        [RelayCommand]
        private async Task TriggerOcr()
        {
            if (_isCapturing) return;

            try
            {
                _isCapturing = true;
                var enabledProfiles = GetEnabledOcrProfiles();
                if (enabledProfiles.Count == 0)
                {
                    if (_dialogService.ShowOcrNotConfiguredDialog())
                    {
                        WeakReferenceMessenger.Default.Send(new OpenSettingsMessage { TabIndex = 2 });
                    }
                    return;
                }

                string? ocrResult = null;
                
                var capturedBytes = await _windowManager.ShowCaptureWindowAsync(async (byte[] bytes, System.Windows.Rect rect) => 
                {
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

                var enabledVisionModels = Config.SavedModels.Where(m => m.IsEnableForScreenshotTranslate).ToList();
                bool useVisionTranslate = enabledVisionModels.Any();

                var enabledOcrProfiles = GetEnabledOcrProfiles();
                var enabledTransProfiles = GetEnabledTranslateProfiles();

                if (!useVisionTranslate)
                {
                    if (enabledOcrProfiles.Count == 0 || enabledTransProfiles.Count == 0)
                    {
                        if (_dialogService.ShowOcrNotConfiguredDialog())
                        {
                            WeakReferenceMessenger.Default.Send(new OpenSettingsMessage { TabIndex = 2 });
                        }
                        return;
                    }
                }

                string? translatedResult = null;
                System.Windows.Rect? actionRect = null;

                var capturedBytes = await _windowManager.ShowCaptureWindowAsync(async (byte[] bytes, System.Windows.Rect rect) =>
                {
                    actionRect = rect;

                    if (useVisionTranslate)
                    {
                        translatedResult = await RaceVisionTranslateAsync(bytes, enabledVisionModels);
                        
                        if (!string.IsNullOrWhiteSpace(translatedResult) && !translatedResult.StartsWith("API Error") && !translatedResult.StartsWith("Vision Error") && !translatedResult.StartsWith("错误"))
                        {
                            return;
                        }
                        if (enabledOcrProfiles.Count == 0 || enabledTransProfiles.Count == 0)
                        {
                            return; 
                        }
                    }

                    var text = await RaceOcrAsync(bytes, enabledOcrProfiles);
                    
                    if (string.IsNullOrWhiteSpace(text) || text.StartsWith("OCR 错误") || text.StartsWith("错误"))
                    {
                        if (translatedResult == null)
                            translatedResult = text ?? "无法识别文字";
                        return;
                    }

                    translatedResult = await RaceTranslateAsync(text, enabledTransProfiles);
                });

                if (capturedBytes == null) return;

                if (string.IsNullOrWhiteSpace(translatedResult) || translatedResult.StartsWith("OCR 错误") || translatedResult.StartsWith("错误") || translatedResult.StartsWith("Vision Error"))
                {
                     if (translatedResult != null && (translatedResult.StartsWith("OCR 错误") || translatedResult.StartsWith("错误") || translatedResult == "无法识别文字" || translatedResult.StartsWith("Vision Error")))
                     {
                         _dialogService.ShowAlert(translatedResult, "识别/翻译失败");
                     }
                     else
                     {
                         _windowManager.ShowTranslationPopup(translatedResult ?? "翻译失败");
                     }
                     return;
                }

                if (Config.AutoCopyTranslationResult && !string.IsNullOrWhiteSpace(translatedResult))
                {
                    try { System.Windows.Clipboard.SetText(translatedResult); } catch { }
                }

                _windowManager.ShowTranslationPopup(translatedResult ?? "翻译失败", actionRect);
            }
            finally
            {
                _isCapturing = false;
            }
        }


        private async Task<string> RaceOcrAsync(byte[] imageBytes, List<ApiProfile> profiles)
        {
            if (imageBytes == null || imageBytes.Length == 0) return "图片数据为空";
            
            var tasks = new List<Task<string>>();

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
                            _ => ""
                        };
                    }
                    catch { return ""; }
                }));
            }

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

            while (tasks.Count > 0)
            {
                var finishedTask = await Task.WhenAny(tasks).ConfigureAwait(false);
                tasks.Remove(finishedTask);

                try
                {
                    var result = await finishedTask.ConfigureAwait(false);
                    if (!string.IsNullOrWhiteSpace(result) && !result.StartsWith("OCR 错误") && !result.StartsWith("错误"))
                    {
                        return result;
                    }
                }
                catch { }
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
3. 不要包含任何开场白（如""图片的文字是...""）。
4. 不要包含任何解释、注脚或Markdown代码块标记。
5. 如果图片中没有文字，输出""无文字""。";

                return await _aiService.ChatWithImageAsync(imageBytes, model.ApiKey, model.BaseUrl, model.ModelName, ocrSystemPrompt).ConfigureAwait(false);
            }
            catch (Exception)
            {
                return "";
            }
        }

        private async Task<string> RaceTranslateAsync(string text, List<ApiProfile> profiles)
        {
            if (string.IsNullOrWhiteSpace(text)) return "";
            
            var tasks = new List<Task<string>>();

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
                            _ => ""
                        };
                    }
                    catch { return ""; }
                }));
            }

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

            while (tasks.Count > 0)
            {
                var finishedTask = await Task.WhenAny(tasks).ConfigureAwait(false);
                tasks.Remove(finishedTask);

                try
                {
                    var result = await finishedTask.ConfigureAwait(false);
                    if (!string.IsNullOrWhiteSpace(result) && 
                        !result.StartsWith("翻译失败") && 
                        !result.StartsWith("错误") && 
                        !result.StartsWith("AI 翻译失败") &&
                        !result.StartsWith("[AI 错误]") &&
                        !result.StartsWith("[系统错误]"))
                    {
                        return result;
                    }
                }
                catch { }
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
            
            if (!string.IsNullOrWhiteSpace(Config.AiTranslationPromptId))
            {
                var msg = new RequestPromptFileMessage { PromptId = Config.AiTranslationPromptId };
                WeakReferenceMessenger.Default.Send(msg);
                if (msg.HasReceivedResponse && msg.Response != null && msg.Response.File != null && !string.IsNullOrWhiteSpace(msg.Response.File.Content))
                {
                    systemPrompt = msg.Response.File.Content;
                }
            }
            return systemPrompt;
        }

        private async Task<string> TranslateWithAiModelAsync(string text, AiModelConfig model, string systemPrompt)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(model.ApiKey)) return "";

                return await _aiService.ChatAsync(text, model.ApiKey, model.BaseUrl, model.ModelName, systemPrompt, model.UseProxy).ConfigureAwait(false) 
                       ?? "";
            }
            catch (Exception)
            {
                return "";
            }
        }

        private async Task<string> RaceVisionTranslateAsync(byte[] imageBytes, List<AiModelConfig> models)
        {
            if (models == null || models.Count == 0) return "未启用 Vision 模型";

            string systemPrompt = GetAiVisionTranslationSystemPrompt();
            var tasks = new List<Task<string>>();

            tasks.AddRange(models.Select(async model =>
            {
                try
                {
                    return await _aiService.ChatWithImageAsync(imageBytes, model.ApiKey, model.BaseUrl, model.ModelName, systemPrompt).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    return $"Vision Error: {ex.Message}";
                }
            }));

            while (tasks.Count > 0)
            {
                var finishedTask = await Task.WhenAny(tasks).ConfigureAwait(false);
                tasks.Remove(finishedTask);

                try
                {
                    var result = await finishedTask.ConfigureAwait(false);
                    if (!string.IsNullOrWhiteSpace(result) && !result.StartsWith("Vision Error") && !result.StartsWith("错误"))
                    {
                        return result;
                    }
                }
                catch { }
            }

            return "Vision Error: 所有 Vision 模型均请求失败";
        }

        private string GetAiVisionTranslationSystemPrompt()
        {
            return @"# Role
您是有史以来最优秀的**图片内容翻译专家**。您具备顶尖的OCR（光学字符识别）能力和语言转换能力。

# Mission
您的核心任务是识别图片中的所有文字，并将其精准、完美地翻译成**简体中文**。

# Guidelines & Constraints (必须严格执行)

## 1. 识别与处理逻辑
- **无文字/无法识别**：如果图片中没有文字或文字模糊无法辨认，直接输出""无文字""。
- **原文即中文**：如果识别到的文字已经是中文，请直接输出原文，不做修改。
- **翻译标准**：
    - 逐句翻译，严格保持原句的语气和意图。
    - 追求信达雅，确保翻译结果为1:1的精准副本，不随意操纵措辞改变原意。

## 2. 输出格式规范
- **纯净输出**：**只输出翻译后的内容**。绝对禁止输出任何""图片中的文字是...""、""这里是翻译...""等废话，也不要包含任何警告或注释。
- **结构保持**：必须严格保持原文的段落结构、换行和格式，不要合并段落或改变排版。

# Workflow
1. 深呼吸，仔细扫描图片的每一个角落。
2. 提取文本。
3. 如果需要翻译，在心中反复推敲最准确的**简体中文**表达。
4. 按照上述""输出格式规范""直接输出最终结果。

## 输入
[在此处上传图片]";
        }

        [RelayCommand]
        private async Task TriggerPinToScreen()
        {
            if (_isCapturing) return;

            try
            {
                _isCapturing = true;
                await _windowManager.ShowPinToScreenFromCaptureAsync();
            }
            finally
            {
                _isCapturing = false;
            }
        }
    }
}

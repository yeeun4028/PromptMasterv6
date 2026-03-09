using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using PromptMasterv6.Core.Interfaces;
using PromptMasterv6.Core.Models;
using PromptMasterv6.Infrastructure.Services;
using PromptMasterv6.Core.Messages;
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
        private readonly IClipboardService _clipboardService;
        
        public ExternalToolsViewModel(
            ISettingsService settingsService,
            IAiService aiService,
            IBaiduService baiduService,
            ITencentService tencentService,
            IGoogleService googleService,
            IDialogService dialogService,
            IWindowManager windowManager,
            IClipboardService clipboardService)
        {
            _settingsService = settingsService;
            _aiService = aiService;
            _baiduService = baiduService;
            _tencentService = tencentService;
            _googleService = googleService;
            _dialogService = dialogService;
            _windowManager = windowManager;
            _clipboardService = clipboardService;
            
            WeakReferenceMessenger.Default.Register<TriggerOcrMessage>(this, async (_, _) => await TriggerOcr());
            WeakReferenceMessenger.Default.Register<TriggerTranslateMessage>(this, async (_, _) => await TriggerTranslate());
            WeakReferenceMessenger.Default.Register<TriggerPinToScreenMessage>(this, async (_, _) => await TriggerPinToScreen());
            
            LoggerService.Instance.LogInfo("ExternalToolsViewModel initialized", "ExternalToolsViewModel.ctor");
            
            EnsureAiProfileExists();
        }
        
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
                string ocrSystemPrompt = _settingsService.Config.OcrPromptTemplate ?? PromptTemplates.Default.OcrSystemPrompt;
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
            string systemPrompt = _settingsService.Config.TranslationPromptTemplate ?? PromptTemplates.Default.TranslationSystemPrompt;
            
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
            return _settingsService.Config.VisionTranslationPromptTemplate ?? PromptTemplates.Default.VisionTranslationSystemPrompt;
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

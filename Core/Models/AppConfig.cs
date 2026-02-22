using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using System.Text.Json.Serialization;
using PromptMasterv5.Infrastructure.Converters;

namespace PromptMasterv5.Core.Models
{
    /// <summary>
    /// 语音识别引擎类型
    /// </summary>
    public enum VoiceProvider
    {
        /// <summary>
        /// OpenAI 兼容 API（录音后转写）
        /// </summary>
        OpenAI = 0,
        
        /// <summary>
        /// 讯飞语音听写（实时流式）
        /// </summary>
        Xunfei = 1
    }

    public partial class AppConfig : ObservableObject
    {
        [ObservableProperty]
        private string webDavUrl = "https://dav.jianguoyun.com/dav/";

        [ObservableProperty]
        private string userName = "";

        [ObservableProperty]
        [property: JsonConverter(typeof(JsonEncryptedStringConverter))]
        private string password = "";

        [ObservableProperty]
        private string globalHotkey = "";

        [ObservableProperty]
        private string singleHotkey = "";

        [ObservableProperty]
        private string fullWindowHotkey = "";

        [ObservableProperty]
        private string miniWindowHotkey = "";

        [ObservableProperty]
        private bool enableDoubleCtrl = true;

        // ★★★ 新增：AI 配置项 ★★★

        [ObservableProperty]
        private string aiBaseUrl = "https://api.deepseek.com";

        [ObservableProperty]
        [property: JsonConverter(typeof(JsonEncryptedStringConverter))]
        private string aiApiKey = "";

        [ObservableProperty]
        private string aiModel = "deepseek-chat";

        [ObservableProperty]
        private ObservableCollection<AiModelConfig> savedModels = new();

        [ObservableProperty]
        private string activeModelId = "";

        // Model selection for Mini Window (empty = use default active model)
        [ObservableProperty]
        private string miniWindowModelId = "";

        // Model selection for Translation (empty = use default active model)
        [ObservableProperty]
        private string translationModelId = "";

        // ★★★ 新增：外部工具 API 配置 ★★★
        [ObservableProperty]
        private System.Collections.ObjectModel.ObservableCollection<ApiProfile> apiProfiles = new();

        [ObservableProperty]
        private string activeApiProfileId = "";

        // 独立的 OCR 配置 ID
        [ObservableProperty]
        private string ocrProfileId = "";

        // 独立的 翻译 配置 ID
        [ObservableProperty]
        private string translateProfileId = "";

        // ★★★ 新增：供应商启用状态 ★★★
        [ObservableProperty]
        private bool enableBaidu = true;

        [ObservableProperty]
        private bool enableTencentCloud = false;

        [ObservableProperty]
        private bool enableYoudao = false;

        [ObservableProperty]
        private bool enableGoogle = false;

        [ObservableProperty]
        private bool enableAiTranslation = false;

        [ObservableProperty]
        private bool autoCopyTranslationResult = true;

        // ★★★ 新增：AI 翻译配置 ★★★
        [ObservableProperty]
        private string aiTranslateBaseUrl = "https://api.deepseek.com";

        [ObservableProperty]
        [property: JsonConverter(typeof(JsonEncryptedStringConverter))]
        private string aiTranslateApiKey = "";

        [ObservableProperty]
        private string aiTranslateModel = "deepseek-chat";

        [ObservableProperty]
        private string aiTranslationPromptId = "";

        [ObservableProperty]
        private ObservableCollection<AiTranslationConfig> savedAiTranslationConfigs = new();

        [ObservableProperty]
        private string activeAiTranslationConfigId = "";

        // External Tools Hotkeys
        [ObservableProperty]
        private string screenshotTranslateHotkey = "";

        [ObservableProperty]
        private string selectedTextTranslateHotkey = "";

        [ObservableProperty]
        private string ocrHotkey = "";

        // Global Quick Action Hotkey
        [ObservableProperty]
        private string quickActionHotkey = "Alt+Q";

        // QuickAction Settings
        [ObservableProperty]
        private QuickActionLongTextMode quickActionLongTextMode = QuickActionLongTextMode.ExternalEditor;

        [ObservableProperty]
        private int quickActionLineThreshold = 15;

        [ObservableProperty]
        private bool quickActionShowText = true;

        [ObservableProperty]
        private bool quickActionShowIcons = true;

        // ★★★ 新增：启动器 (Launcher) 配置 ★★★
        [ObservableProperty]
        private string launcherHotkey = "Alt+S";

        [ObservableProperty]
        private bool launcherRunAsAdmin = false;

        [ObservableProperty]
        private ObservableCollection<string> launcherSearchPaths = new();


        [ObservableProperty]
        private ObservableCollection<WebTarget> webDirectTargets = new();

        [ObservableProperty]
        private string defaultWebTargetName = "Gemini";

        [ObservableProperty]
        private bool enableDoubleEnterSend = true;

        public string RemoteFolderName { get; set; } = "PromptMaster";

        // ★★★ 新增：语音控制 (Voice Control) 配置 ★★★
        [ObservableProperty]
        private string voiceApiBaseUrl = "https://api.openai.com/v1";

        [ObservableProperty]
        [property: JsonConverter(typeof(JsonEncryptedStringConverter))]
        private string voiceApiKey = "";

        [ObservableProperty]
        private string voiceApiModel = "whisper-1";

        [ObservableProperty]
        private string voiceTriggerHotkey = "F1+T";

        [ObservableProperty]
        private string voiceCommandConfigPath = "voice_commands.json";

        [ObservableProperty]
        private string voiceModelId = "";

        // ★★★ 讯飞语音听写配置 ★★★

        /// <summary>
        /// 语音识别引擎类型
        /// </summary>
        [ObservableProperty]
        private VoiceProvider voiceProvider = VoiceProvider.OpenAI;

        /// <summary>
        /// 讯飞 AppID
        /// </summary>
        [ObservableProperty]
        private string xunfeiAppId = "";

        /// <summary>
        /// 讯飞 API Key
        /// </summary>
        [ObservableProperty]
        [property: JsonConverter(typeof(JsonEncryptedStringConverter))]
        private string xunfeiApiKey = "";

        /// <summary>
        /// 讯飞 API Secret
        /// </summary>
        [ObservableProperty]
        [property: JsonConverter(typeof(JsonEncryptedStringConverter))]
        private string xunfeiApiSecret = "";

        /// <summary>
        /// 静音检测超时（毫秒）
        /// </summary>
        [ObservableProperty]
        private int xunfeiVadEos = 2000;

        /// <summary>
        /// 自动添加标点
        /// </summary>
        [ObservableProperty]
        private bool xunfeiEnablePunctuation = true;

        /// <summary>
        /// 显示中间结果
        /// </summary>
        [ObservableProperty]
        private bool xunfeiEnableIntermediateResult = true;

        /// <summary>
        /// 录音时降低系统音量
        /// </summary>
        [ObservableProperty]
        private bool voiceDuckVolume = true;
    }
}

using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using System.Text.Json.Serialization;
using PromptMasterv6.Infrastructure.Converters;

namespace PromptMasterv6.Core.Models
{
    public enum LaunchBarActionType
    {
        BuiltIn,
        CustomApp
    }

    public partial class LaunchBarItem : ObservableObject
    {
        [ObservableProperty]
        private string id = Guid.NewGuid().ToString();

        [ObservableProperty]
        private string colorHex = "#FF4183C4";

        [ObservableProperty]
        private LaunchBarActionType actionType = LaunchBarActionType.BuiltIn;

        [ObservableProperty]
        private string actionTarget = "ToggleWindow";

        [ObservableProperty]
        private string label = "新建功能";
    }

    public partial class AppConfig : ObservableObject
    {
        [ObservableProperty]
        private bool enableLaunchBar = true;

        [ObservableProperty]
        private double launchBarWidth = 6.0;

        [ObservableProperty]
        private string launchBarHotkey = "Alt+L";

        [ObservableProperty]
        private ObservableCollection<LaunchBarItem> launchBarItems = new();
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

        [ObservableProperty] private bool autoHide = true;

        [ObservableProperty] private int autoHideDelay = 10;

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

        // OCR Hotkey
        [ObservableProperty]
        private string ocrHotkey = "";

        // PinToScreen Hotkey
        [ObservableProperty]
        private string pinToScreenHotkey = "";

        // ★★★ 新增：启动器 (Launcher) 配置 ★★★
        [ObservableProperty]
        private string launcherHotkey = "Alt+S";

        [ObservableProperty]
        private bool launcherRunAsAdmin = false;

        [ObservableProperty]
        private ObservableCollection<string> launcherSearchPaths = new();

        [ObservableProperty]
        private bool isLauncherSinglePageDisplayEnabled = true;

        [ObservableProperty]
        private ObservableCollection<WebTarget> webDirectTargets = new();

        [ObservableProperty]
        private string defaultWebTargetName = "Gemini";

        [ObservableProperty]
        private bool enableDoubleEnterSend = true;

        public string RemoteFolderName { get; set; } = "PromptMaster";

        [ObservableProperty]
        private string proxyAddress = "http://127.0.0.1:10808";

        // ★★★ 主窗口位置和大小 ★★★

        /// <summary>
        /// 主窗口 X 坐标
        /// </summary>
        [ObservableProperty]
        private double mainWindowLeft = -1.0;  // -1 = 未设置（避免 NaN 无法序列化）

        /// <summary>
        /// 主窗口 Y 坐标
        /// </summary>
        [ObservableProperty]
        private double mainWindowTop = -1.0;   // -1 = 未设置（避免 NaN 无法序列化）

        /// <summary>
        /// 主窗口宽度
        /// </summary>
        [ObservableProperty]
        private double mainWindowWidth = 900;

        /// <summary>
        /// 主窗口高度
        /// </summary>
        [ObservableProperty]
        private double mainWindowHeight = 600;

        /// <summary>
        /// 主窗口是否最大化
        /// </summary>
        [ObservableProperty]
        private bool mainWindowMaximized = false;

        /// <summary>
        /// 净化所有 double 字段，将 Infinity / NaN 替换为安全默认值。
        /// 在从磁盘加载配置后调用，防止损坏的 config.json 导致序列化崩溃。
        /// </summary>
        public void Sanitize()
        {
            // 将 Infinity/NaN 转换为 -1（"未设置"哨兵值），-1 是合法 JSON 数字
            if (!double.IsFinite(MainWindowLeft))   MainWindowLeft   = -1.0;
            if (!double.IsFinite(MainWindowTop))    MainWindowTop    = -1.0;
            if (!double.IsFinite(MainWindowWidth)  || MainWindowWidth  <= 0) MainWindowWidth  = 900;
            if (!double.IsFinite(MainWindowHeight) || MainWindowHeight <= 0) MainWindowHeight = 600;
            if (!double.IsFinite(LaunchBarWidth)   || LaunchBarWidth   <= 0) LaunchBarWidth   = 6.0;
        }
    }
}

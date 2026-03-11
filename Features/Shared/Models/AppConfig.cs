using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using System.Text.Json.Serialization;
using PromptMasterv6.Core.Converters;

namespace PromptMasterv6.Features.Shared.Models;

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

    [ObservableProperty]
    private System.Collections.ObjectModel.ObservableCollection<ApiProfile> apiProfiles = new();

    [ObservableProperty]
    private string ocrProfileId = "";

    [ObservableProperty]
    private string translateProfileId = "";

    [ObservableProperty]
    private bool autoCopyTranslationResult = true;

    [ObservableProperty]
    private string aiTranslationPromptId = "";

    [ObservableProperty]
    private ObservableCollection<AiTranslationConfig> savedAiTranslationConfigs = new();

    [ObservableProperty]
    private string screenshotTranslateHotkey = "";

    [ObservableProperty]
    private string ocrHotkey = "";

    [ObservableProperty]
    private string pinToScreenHotkey = "";

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

    [ObservableProperty]
    private string? ocrPromptTemplate;

    [ObservableProperty]
    private string? translationPromptTemplate;

    [ObservableProperty]
    private string? visionTranslationPromptTemplate;

    public string RemoteFolderName { get; set; } = "PromptMasterv6";

    [ObservableProperty]
    private string proxyAddress = "http://127.0.0.1:10808";

    [ObservableProperty]
    private double mainWindowLeft = -1.0;

    [ObservableProperty]
    private double mainWindowTop = -1.0;

    [ObservableProperty]
    private double mainWindowWidth = 900;

    [ObservableProperty]
    private double mainWindowHeight = 600;

    [ObservableProperty]
    private bool mainWindowMaximized = false;

    public void Sanitize()
    {
        if (!double.IsFinite(MainWindowLeft))   MainWindowLeft   = -1.0;
        if (!double.IsFinite(MainWindowTop))    MainWindowTop    = -1.0;
        if (!double.IsFinite(MainWindowWidth)  || MainWindowWidth  <= 0) MainWindowWidth  = 900;
        if (!double.IsFinite(MainWindowHeight) || MainWindowHeight <= 0) MainWindowHeight = 600;
        if (!double.IsFinite(LaunchBarWidth)   || LaunchBarWidth   <= 0) LaunchBarWidth   = 6.0;
    }
}

public partial class AiTranslationConfig : ObservableObject
{
    [ObservableProperty]
    private string id = Guid.NewGuid().ToString();

    [ObservableProperty]
    private string promptId = "";

    [ObservableProperty]
    private string promptTitle = "";

    [ObservableProperty]
    private string baseUrl = "";

    [ObservableProperty]
    private string apiKey = "";

    [ObservableProperty]
    private string model = "";
}

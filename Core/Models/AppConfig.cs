using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace PromptMasterv5.Core.Models
{
    public partial class AppConfig : ObservableObject
    {
        [ObservableProperty]
        private string webDavUrl = "https://dav.jianguoyun.com/dav/";

        [ObservableProperty]
        private string userName = "";

        [ObservableProperty]
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
        private string aiApiKey = "";

        [ObservableProperty]
        private string aiModel = "deepseek-chat";

        [ObservableProperty]
        private ObservableCollection<AiModelConfig> savedModels = new();

        [ObservableProperty]
        private string activeModelId = "";

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

        public string RemoteFolderName { get; set; } = "PromptMaster";
    }
}

using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace PromptMasterv5.Models
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
        private string globalHotkey = "Alt+Space";

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

        public string RemoteFolderName { get; set; } = "PromptMaster";
    }
}
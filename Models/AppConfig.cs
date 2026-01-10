using CommunityToolkit.Mvvm.ComponentModel;

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

        // ★★★ 修复关键：补上这个缺失的属性 ★★★
        [ObservableProperty]
        private bool enableDoubleCtrl = true;

        public string RemoteFolderName { get; set; } = "PromptMaster";
    }
}
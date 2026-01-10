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

        // ★★★ 新增：全局唤醒热键 (默认 Alt+Space) ★★★
        // 这就是之前报错缺少的属性
        [ObservableProperty]
        private string globalHotkey = "Alt+Space";

        public string RemoteFolderName { get; set; } = "PromptMaster";
    }
}
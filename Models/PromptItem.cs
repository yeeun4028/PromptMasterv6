using CommunityToolkit.Mvvm.ComponentModel;
using System;

namespace PromptMasterv5.Models
{
    public partial class PromptItem : ObservableObject
    {
        [ObservableProperty]
        private string title = "";

        [ObservableProperty]
        private string content = "";

        [ObservableProperty]
        private DateTime lastModified;

        // ★ 新增：所属文件夹的ID
        public string FolderId { get; set; } = "";
    }
}
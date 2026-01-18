using CommunityToolkit.Mvvm.ComponentModel;
using System;

namespace PromptMasterv5.Models
{
    public partial class FolderItem : ObservableObject
    {
        // 文件夹唯一标识
        public string Id { get; set; } = Guid.NewGuid().ToString();

        // 文件夹名称
        [ObservableProperty]
        private string name = string.Empty;

        // ★★★ 新增：自定义图标 SVG 路径代码 ★★★
        // 存储用于 UI 显示的 Path Data
        [ObservableProperty]
        private string? iconGeometry;
    }
}
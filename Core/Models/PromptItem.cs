using CommunityToolkit.Mvvm.ComponentModel;
using System;

namespace PromptMasterv6.Core.Models
{
    public partial class PromptItem : ObservableObject
    {
        // 文件唯一标识
        public string Id { get; set; } = Guid.NewGuid().ToString();

        // 文件标题
        [ObservableProperty]
        private string title = string.Empty;

        // 文件内容 (Markdown)
        [ObservableProperty]
        private string? content;

        // 最后修改时间
        [ObservableProperty]
        private DateTime lastModified;

        // 所属文件夹ID
        public string? FolderId { get; set; }

        // ★★★ 新增：自定义图标 SVG 路径代码 ★★★
        [ObservableProperty]
        private string? iconGeometry;

        // ★★★ 新增：描述信息 ★★★
        [ObservableProperty]
        private string? description;

        // ★★★ 新增：创建时间 ★★★
        [ObservableProperty]
        private DateTime createdAt = DateTime.Now;

        // ★★★ 新增：更新时间（兼容 LastModified） ★★★
        // 实际上我们可以用 LastModified 作为 UpdatedAt 的别名，或者显式添加 UpdatedAt
        [ObservableProperty]
        private DateTime updatedAt = DateTime.Now;

    }
}
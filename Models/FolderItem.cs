using CommunityToolkit.Mvvm.ComponentModel;
using System;

namespace PromptMasterv5.Models
{
    public partial class FolderItem : ObservableObject
    {
        public string Id { get; set; } = Guid.NewGuid().ToString(); // 唯一ID

        [ObservableProperty]
        private string name = "";

        [ObservableProperty]
        private bool isSelected;
    }
}
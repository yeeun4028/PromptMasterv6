using System.Collections.Generic;

namespace PromptMasterv5.Core.Models
{
    public class AppData
    {
        public List<FolderItem> Folders { get; set; } = new();
        public List<PromptItem> Files { get; set; } = new();
    }
}

using System;

namespace PromptMasterv5.Core.Models
{
    public class BackupFileItem
    {
        public string FilePath { get; set; } = "";
        public string FileName { get; set; } = "";
        public DateTime LastModified { get; set; }
        public long Size { get; set; }
        
        public string DisplayText => $"{FileName} ({Size / 1024.0:F1} KB) - {LastModified:yyyy-MM-dd HH:mm:ss}";
    }
}

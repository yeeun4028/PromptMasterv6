using System;

namespace PromptMasterv5.Core.Models
{
    public class BackupFileItem
    {
        public string FilePath { get; set; } = "";
        public string FileName { get; set; } = "";
        public DateTime LastModified { get; set; }
        public long Size { get; set; }

        public string DisplaySize => Size < 1024 * 1024 
            ? $"{Size / 1024.0:F1} KB" 
            : $"{Size / (1024.0 * 1024.0):F2} MB";
        
        public string DisplayText => $"{FileName} ({DisplaySize}) - {LastModified:yyyy-MM-dd HH:mm:ss}";
    }
}

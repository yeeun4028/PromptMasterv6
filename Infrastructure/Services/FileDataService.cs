using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using PromptMasterv5.Core.Models;
using PromptMasterv5.Core.Interfaces;

namespace PromptMasterv5.Infrastructure.Services
{
    public class FileDataService : IDataService
    {
        private readonly string _filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data.json");
        public string BackupDirectory => Path.GetDirectoryName(_filePath) ?? "";

        public async Task SaveAsync(IEnumerable<FolderItem> folders, IEnumerable<PromptItem> files)
        {
            var data = new AppData
            {
                Folders = new List<FolderItem>(folders),
                Files = new List<PromptItem>(files)
            };
            var options = new JsonSerializerOptions { WriteIndented = true };

            string tempPath = _filePath + ".tmp";
            
            try
            {
                // 1. Write to temp file
                using (FileStream createStream = File.Create(tempPath))
                {
                    await JsonSerializer.SerializeAsync(createStream, data, options);
                }

                // 2. Perform safe rotation (Atomic Write)
                if (File.Exists(_filePath))
                {
                    RotateBackups(_filePath, 5);
                }

                File.Move(tempPath, _filePath, overwrite: true);
            }
            catch (Exception)
            {
                // Verify if temp file exists and delete it if operation failed
                try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { }
            }
        }

        private void RotateBackups(string originalPath, int maxBackups)
        {
            try 
            {
                // Delete the oldest backup
                string oldestBackup = $"{originalPath}.bak{maxBackups}";
                if (File.Exists(oldestBackup)) File.Delete(oldestBackup);

                // Shift existing backups down
                for (int i = maxBackups - 1; i >= 1; i--)
                {
                    string source = $"{originalPath}.bak{i}";
                    string dest = $"{originalPath}.bak{i + 1}";
                    if (File.Exists(source)) File.Move(source, dest);
                }

                // Move current file to bak1
                string bak1 = $"{originalPath}.bak1";
                if (File.Exists(originalPath)) File.Move(originalPath, bak1);
            }
            catch { }
        }

        public List<BackupFileItem> GetBackups()
        {
            var backups = new List<BackupFileItem>();
            try
            {
                var dir = Path.GetDirectoryName(_filePath);
                if (string.IsNullOrEmpty(dir)) return backups;

                var files = Directory.GetFiles(dir, "data.json*");
                foreach (var file in files)
                {
                    var info = new FileInfo(file);
                    backups.Add(new BackupFileItem
                    {
                        FilePath = file,
                        FileName = info.Name,
                        LastModified = info.LastWriteTime,
                        Size = info.Length
                    });
                }
            }
            catch { }
            return backups.OrderByDescending(x => x.LastModified).ToList();
        }

        public async Task<AppData?> RestoreLocalBackupAsync(string backupPath)
        {
            if (!File.Exists(backupPath)) return null;

            try
            {
                // Verify content
                using FileStream openStream = File.OpenRead(backupPath);
                var data = await JsonSerializer.DeserializeAsync<AppData>(openStream);

                if (data != null)
                {
                    // Copy to data.json
                    File.Copy(backupPath, _filePath, overwrite: true);
                    return data;
                }
            }
            catch { }
            return null;
        }

        public async Task<AppData> LoadAsync()
        {
            if (!File.Exists(_filePath)) return new AppData();

            try
            {
                using FileStream openStream = File.OpenRead(_filePath);
                return await JsonSerializer.DeserializeAsync<AppData>(openStream) ?? new AppData();
            }
            catch (Exception)
            {
                return new AppData();
            }
        }
    }
}

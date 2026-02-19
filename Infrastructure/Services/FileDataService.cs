using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

        public async Task SaveAsync(IEnumerable<FolderItem> folders, IEnumerable<PromptItem> files, Dictionary<string, string> voiceCommands)
        {
            var data = new AppData
            {
                Folders = new List<FolderItem>(folders),
                Files = new List<PromptItem>(files),
                VoiceCommands = voiceCommands ?? new()
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
                
                // 记录保存成功
                LoggerService.Instance.LogInfo($"数据已保存到 {_filePath} ({data.Files.Count} 个提示词)", "FileDataService.SaveAsync");
            }
            catch (Exception ex)
            {
                // 记录保存失败
                LoggerService.Instance.LogException(ex, $"保存数据到 {_filePath} 失败", "FileDataService.SaveAsync");
                
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
                var data = await JsonSerializer.DeserializeAsync<AppData>(openStream) ?? new AppData();
                
                // 记录加载成功
                LoggerService.Instance.LogInfo($"从 {_filePath} 加载了 {data.Files?.Count ?? 0} 个提示词", "FileDataService.LoadAsync");
                
                return data;
            }
            catch (Exception ex)
            {
                // 记录加载失败
                LoggerService.Instance.LogException(ex, $"从 {_filePath} 加载数据失败", "FileDataService.LoadAsync");
                return new AppData();
            }
        }

        public async Task<IEnumerable<PromptItem>> GetQuickActionsAsync()
        {
            var data = await LoadAsync();
            return data.Files.FindAll(f => f.IsQuickAction);
        }

        public async Task ArchiveQuickActionHistoryAsync(string userQuestion, string aiResponse)
        {
            var data = await LoadAsync();
            
            // 1. Find or create "Quick Actions" topic
            // Assuming we are storing topics in a separate structure or within a special folder/file structure
            // For simplicity in this v5 architecture (based on AppData structure), let's assume we use a special Folder named "Quick Actions History"
            // Or better, if there's a Chat History mechanism, use that.
            // Since AppData has Folders and Files (PromptItem), and PromptItem seems to be a prompt template,
            // we might need a different place for Chat History.
            
            // Checking AppData structure... it seems PromptMasterv5 is prompt manager, maybe Chat history is not fully implemented in AppData?
            // Let's check ChatViewModel or similar to see how history is stored.
            // If no existing history storage, we can create a text file log or append to a special PromptItem content.
            
            // Strategy: Create/Update a PromptItem named "Quick Actions History"
            var historyItem = data.Files.FirstOrDefault(f => f.Title == "Quick Actions History");
            if (historyItem == null)
            {
                historyItem = new PromptItem
                {
                    Id = Guid.NewGuid().ToString(),
                    Title = "Quick Actions History",
                    Content = "",
                    Description = "Auto-archived history from Global Quick Actions",
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                };
                data.Files.Add(historyItem);
            }
            
            // Append new conversation
            var newEntry = $"## {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n**Q:** {userQuestion}\n\n**A:** {aiResponse}\n\n---\n\n";
            historyItem.Content = newEntry + historyItem.Content; // Prepend to show newest first
            
            // Limit length (optional, simple char limit for now)
            if (historyItem.Content.Length > 50000)
            {
                historyItem.Content = historyItem.Content.Substring(0, 50000);
            }
            
            historyItem.UpdatedAt = DateTime.Now;
            
            await SaveAsync(data.Folders, data.Files, data.VoiceCommands);
        }
    }
}

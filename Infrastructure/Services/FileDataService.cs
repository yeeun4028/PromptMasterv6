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

        public async Task SaveAsync(IEnumerable<FolderItem> folders, IEnumerable<PromptItem> files, Dictionary<string, VoiceCommand> voiceCommands)
        {
            var config = ConfigService.Load();
            var data = new AppData
            {
                Folders = new List<FolderItem>(folders),
                Files = new List<PromptItem>(files),
                VoiceCommandsV2 = voiceCommands ?? new(),
                ApiProfiles = new List<ApiProfile>(config.ApiProfiles),
                SavedModels = new List<AiModelConfig>(config.SavedModels)
            };
            var options = new JsonSerializerOptions 
            { 
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };

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
                
                // --- 新增：自动导出所有的提示词为 .md 文件 ---
                _ = Task.Run(() => ExportPromptsToMarkdown(folders, files));

                // 记录保存成功
                LoggerService.Instance.LogInfo($"数据已保存到 {_filePath} ({data.Files.Count} 个提示词)", "FileDataService.SaveAsync");
            }
            catch (Exception ex)
            {
                // 记录保存失败
                LoggerService.Instance.LogException(ex, $"保存数据到 {_filePath} 失败", "FileDataService.SaveAsync");
                
                // Verify if temp file exists and delete it if operation failed
                try { if (File.Exists(tempPath)) File.Delete(tempPath); }
                catch (Exception delEx)
                {
                    LoggerService.Instance.LogException(delEx, $"Failed to delete temp file: {tempPath}", "FileDataService.SaveAsync");
                }
            }
        }

        private void ExportPromptsToMarkdown(IEnumerable<FolderItem> folders, IEnumerable<PromptItem> files)
        {
            try
            {
                string exportDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MyPrompts");
                if (!Directory.Exists(exportDir))
                {
                    Directory.CreateDirectory(exportDir);
                }

                var folderDict = folders.ToDictionary(f => f.Id, f => f.Name);
                var generatedFileNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                // 1. 获取目前 MyPrompts 文件夹里所有的现存 .md 文件，准备做对比删除
                var existingFiles = new HashSet<string>(Directory.GetFiles(exportDir, "*.md"), StringComparer.OrdinalIgnoreCase);
                var filesToKeep = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                // 2. 遍历保存每一个提示词
                foreach (var file in files)
                {
                    if (string.IsNullOrWhiteSpace(file.Title)) continue;

                    string folderName = "";
                    if (!string.IsNullOrEmpty(file.FolderId) && folderDict.TryGetValue(file.FolderId, out var fName))
                    {
                        folderName = $"[{fName}] ";
                    }

                    // 构造基础文件名： [文件夹名] 标题
                    string rawName = $"{folderName}{file.Title}";
                    
                    // 替换非法字符
                    string safeName = string.Join("_", rawName.Split(Path.GetInvalidFileNameChars()));

                    // 处理重名 (如果有完全重名的，在其后追加 (1), (2)...)
                    string finalName = safeName;
                    int counter = 1;
                    while (generatedFileNames.Contains(finalName))
                    {
                        finalName = $"{safeName} ({counter})";
                        counter++;
                    }
                    generatedFileNames.Add(finalName);

                    string filePath = Path.Combine(exportDir, $"{finalName}.md");
                    filesToKeep.Add(filePath);

                    // 只有当文件不存在，或者内容有差异时才写入，减少磁盘损耗
                    bool shouldWrite = true;
                    if (File.Exists(filePath))
                    {
                        string existingContent = File.ReadAllText(filePath);
                        if (existingContent == (file.Content ?? ""))
                        {
                            shouldWrite = false;
                        }
                    }

                    if (shouldWrite)
                    {
                        File.WriteAllText(filePath, file.Content ?? "");
                    }
                }
            }
            catch (Exception ex)
            {
                LoggerService.Instance.LogException(ex, "Failed to export prompts to markdown", "FileDataService.ExportPromptsToMarkdown");
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
            catch (Exception ex)
            {
                LoggerService.Instance.LogException(ex, "Failed to rotate backups", "FileDataService.RotateBackups");
            }
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
            catch (Exception ex)
            {
                LoggerService.Instance.LogException(ex, "Failed to get backups", "FileDataService.GetBackups");
            }
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
            catch (Exception ex)
            {
                LoggerService.Instance.LogException(ex, $"Failed to restore backup: {backupPath}", "FileDataService.RestoreLocalBackupAsync");
            }
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

    }
}

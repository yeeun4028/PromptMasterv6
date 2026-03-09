using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using PromptMasterv6.Core.Interfaces;

namespace PromptMasterv6.Infrastructure.Services
{
    public class FileDataService : IDataService
    {
        private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        private readonly string _filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data.json");
        public string BackupDirectory => Path.GetDirectoryName(_filePath) ?? "";

        public async Task SaveAsync(IEnumerable<FolderItem> folders, IEnumerable<PromptItem> files, CancellationToken cancellationToken = default)
        {
            var config = ConfigService.Load();
            var data = new AppData
            {
                Folders = new List<FolderItem>(folders),
                Files = new List<PromptItem>(files),
                ApiProfiles = new List<ApiProfile>(config.ApiProfiles),
                SavedModels = new List<AiModelConfig>(config.SavedModels)
            };

            string tempPath = _filePath + ".tmp";
            
            try
            {
                using (FileStream createStream = File.Create(tempPath))
                {
                    await JsonSerializer.SerializeAsync(createStream, data, _jsonOptions, cancellationToken).ConfigureAwait(false);
                }

                if (File.Exists(_filePath))
                {
                    RotateBackups(_filePath, 5);
                }

                File.Move(tempPath, _filePath, overwrite: true);
                
                _ = Task.Run(() => ExportPromptsToMarkdown(folders, files), cancellationToken);

                LoggerService.Instance.LogInfo($"数据已保存到 {_filePath} ({data.Files.Count} 个提示词)", "FileDataService.SaveAsync");
            }
            catch (Exception ex)
            {
                LoggerService.Instance.LogException(ex, $"保存数据到 {_filePath} 失败", "FileDataService.SaveAsync");
                
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

                var existingFiles = new HashSet<string>(Directory.GetFiles(exportDir, "*.md"), StringComparer.OrdinalIgnoreCase);
                var filesToKeep = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var file in files)
                {
                    if (string.IsNullOrWhiteSpace(file.Title)) continue;

                    string folderName = "";
                    if (!string.IsNullOrEmpty(file.FolderId) && folderDict.TryGetValue(file.FolderId, out var fName))
                    {
                        folderName = $"[{fName}] ";
                    }

                    string rawName = $"{folderName}{file.Title}";
                    string safeName = string.Join("_", rawName.Split(Path.GetInvalidFileNameChars()));

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
                string oldestBackup = $"{originalPath}.bak{maxBackups}";
                if (File.Exists(oldestBackup)) File.Delete(oldestBackup);

                for (int i = maxBackups - 1; i >= 1; i--)
                {
                    string source = $"{originalPath}.bak{i}";
                    string dest = $"{originalPath}.bak{i + 1}";
                    if (File.Exists(source)) File.Move(source, dest);
                }

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
                using FileStream openStream = File.OpenRead(backupPath);
                var data = await JsonSerializer.DeserializeAsync<AppData>(openStream).ConfigureAwait(false);

                if (data != null)
                {
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

        public async Task<AppData> LoadAsync(CancellationToken cancellationToken = default)
        {
            if (!File.Exists(_filePath)) return new AppData();

            try
            {
                using FileStream openStream = File.OpenRead(_filePath);
                var data = await JsonSerializer.DeserializeAsync<AppData>(openStream, _jsonOptions, cancellationToken).ConfigureAwait(false) ?? new AppData();
                
                LoggerService.Instance.LogInfo($"从 {_filePath} 加载了 {data.Files?.Count ?? 0} 个提示词", "FileDataService.LoadAsync");
                
                return data;
            }
            catch (Exception ex)
            {
                LoggerService.Instance.LogException(ex, $"从 {_filePath} 加载数据失败", "FileDataService.LoadAsync");
                return new AppData();
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using PromptMasterv5.Models;

namespace PromptMasterv5.Services
{
    public class FileDataService : IDataService
    {
        private readonly string _filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data.json");

        public void Save(IEnumerable<FolderItem> folders, IEnumerable<PromptItem> files)
        {
            var data = new AppData
            {
                Folders = new List<FolderItem>(folders),
                Files = new List<PromptItem>(files)
            };

            var options = new JsonSerializerOptions { WriteIndented = true };

            try
            {
                string jsonString = JsonSerializer.Serialize(data, options);
                File.WriteAllText(_filePath, jsonString);
            }
            catch (Exception)
            {
                // TODO: 生产环境应记录日志
            }
        }

        public AppData Load()
        {
            if (!File.Exists(_filePath)) return new AppData();

            try
            {
                string jsonString = File.ReadAllText(_filePath);
                return JsonSerializer.Deserialize<AppData>(jsonString) ?? new AppData();
            }
            catch (Exception)
            {
                return new AppData();
            }
        }
    }
}
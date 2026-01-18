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

        public async Task SaveAsync(IEnumerable<FolderItem> folders, IEnumerable<PromptItem> files)
        {
            var data = new AppData
            {
                Folders = new List<FolderItem>(folders),
                Files = new List<PromptItem>(files)
            };

            var options = new JsonSerializerOptions { WriteIndented = true };

            try
            {
                using FileStream createStream = File.Create(_filePath);
                await JsonSerializer.SerializeAsync(createStream, data, options);
            }
            catch (Exception)
            {
            }
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

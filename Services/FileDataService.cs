using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using PromptMasterv5.Core.Models;
using PromptMasterv5.Core.Interfaces;

namespace PromptMasterv5.Services
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
                // 使用异步流写入，不卡界面
                using FileStream createStream = File.Create(_filePath);
                await JsonSerializer.SerializeAsync(createStream, data, options);
            }
            catch (Exception)
            {
                // TODO: 记录日志
            }
        }

        public async Task<AppData> LoadAsync()
        {
            if (!File.Exists(_filePath)) return new AppData();

            try
            {
                // 异步读取
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
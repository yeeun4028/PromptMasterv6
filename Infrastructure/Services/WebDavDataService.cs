using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using PromptMasterv5.Core.Models;
using PromptMasterv5.Core.Interfaces;
using WebDav;

namespace PromptMasterv5.Infrastructure.Services
{
    public class WebDavDataService : IDataService
    {
        private WebDavClient GetClient(AppConfig config)
        {
            return new WebDavClient(new WebDavClientParams
            {
                BaseAddress = new Uri(config.WebDavUrl),
                Credentials = new System.Net.NetworkCredential(config.UserName, config.Password)
            });
        }

        public async Task SaveAsync(IEnumerable<FolderItem> folders, IEnumerable<PromptItem> files, Dictionary<string, string> voiceCommands)
        {
            var config = ConfigService.Load();
            if (string.IsNullOrEmpty(config.UserName) || string.IsNullOrEmpty(config.Password))
            {
                throw new Exception("请先在设置中填写 WebDAV 账号和密码！");
            }

            var client = GetClient(config);
            string remotePath = $"{config.RemoteFolderName}/PromptMasterData.json";

            var data = new AppData
            {
                Folders = new List<FolderItem>(folders),
                Files = new List<PromptItem>(files),
                VoiceCommands = voiceCommands ?? new()
            };
            var options = new JsonSerializerOptions { WriteIndented = true };
            string jsonString = JsonSerializer.Serialize(data, options);
            using var contentStream = new MemoryStream(Encoding.UTF8.GetBytes(jsonString));

            var response = await client.PutFile(remotePath, contentStream);

            if (!response.IsSuccessful)
            {
                throw new Exception($"WebDAV错误 {response.StatusCode}: {response.Description}");
            }
        }

        public async Task<AppData> LoadAsync()
        {
            var config = ConfigService.Load();
            if (string.IsNullOrEmpty(config.UserName) || string.IsNullOrEmpty(config.Password))
            {
                return new AppData();
            }

            var client = GetClient(config);
            string remotePath = $"{config.RemoteFolderName}/PromptMasterData.json";

            try
            {
                var response = await client.GetRawFile(remotePath);
                if (!response.IsSuccessful) return new AppData();

                using var streamReader = new StreamReader(response.Stream);
                string jsonString = await streamReader.ReadToEndAsync();
                return JsonSerializer.Deserialize<AppData>(jsonString) ?? new AppData();
            }
            catch
            {
                return new AppData();
            }
        }

        public async Task<IEnumerable<PromptItem>> GetQuickActionsAsync()
        {
            // For now, load all and filter. In future, we might cache this.
            var data = await LoadAsync();
            return data.Files.FindAll(f => f.IsQuickAction);
        }

        public async Task ArchiveQuickActionHistoryAsync(string userQuestion, string aiResponse)
        {
            var data = await LoadAsync();
            
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
            
            var newEntry = $"## {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n**Q:** {userQuestion}\n\n**A:** {aiResponse}\n\n---\n\n";
            historyItem.Content = newEntry + historyItem.Content;
            
            if (historyItem.Content.Length > 50000)
            {
                historyItem.Content = historyItem.Content.Substring(0, 50000);
            }
            
            historyItem.UpdatedAt = DateTime.Now;
            
            await SaveAsync(data.Folders, data.Files, data.VoiceCommands);
        }
    }
}

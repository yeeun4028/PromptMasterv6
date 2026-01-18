using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using PromptMasterv5.Core.Models;
using PromptMasterv5.Core.Interfaces;
using WebDav;

namespace PromptMasterv5.Services
{
    public class WebDavDataService : IDataService
    {
        // 这里不再写死密码，而是去读配置
        private WebDavClient GetClient(AppConfig config)
        {
            return new WebDavClient(new WebDavClientParams
            {
                BaseAddress = new Uri(config.WebDavUrl),
                Credentials = new System.Net.NetworkCredential(config.UserName, config.Password)
            });
        }

        public async Task SaveAsync(IEnumerable<FolderItem> folders, IEnumerable<PromptItem> files)
        {
            // 1. 读取最新配置
            var config = ConfigService.Load();
            if (string.IsNullOrEmpty(config.UserName) || string.IsNullOrEmpty(config.Password))
            {
                throw new Exception("请先在设置中填写 WebDAV 账号和密码！");
            }

            var client = GetClient(config);
            string remotePath = $"{config.RemoteFolderName}/PromptMasterData.json";

            // 2. 准备数据
            var data = new AppData
            {
                Folders = new List<FolderItem>(folders),
                Files = new List<PromptItem>(files)
            };
            var options = new JsonSerializerOptions { WriteIndented = true };
            string jsonString = JsonSerializer.Serialize(data, options);
            using var contentStream = new MemoryStream(Encoding.UTF8.GetBytes(jsonString));

            // 3. 上传
            var response = await client.PutFile(remotePath, contentStream);

            if (!response.IsSuccessful)
            {
                throw new Exception($"WebDAV错误 {response.StatusCode}: {response.Description}");
            }
        }

        public async Task<AppData> LoadAsync()
        {
            // 1. 读取配置
            var config = ConfigService.Load();
            // 如果没填密码，直接返回空数据，不报错，以免软件启动崩溃
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
    }
}
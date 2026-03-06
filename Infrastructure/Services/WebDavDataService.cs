using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using PromptMasterv6.Core.Models;
using PromptMasterv6.Core.Interfaces;
using WebDav;

namespace PromptMasterv6.Infrastructure.Services
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

        public async Task SaveAsync(IEnumerable<FolderItem> folders, IEnumerable<PromptItem> files)
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
                ApiProfiles = new List<ApiProfile>(),
                SavedModels = new List<AiModelConfig>()
            };
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };
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

    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using PromptMasterv6.Core.Interfaces;
using WebDav;

namespace PromptMasterv6.Infrastructure.Services
{
    public class WebDavDataService : IDataService
    {
        private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        private readonly LoggerService _logger;

        public WebDavDataService(LoggerService logger)
        {
            _logger = logger;
        }

        private WebDavClient GetClient(AppConfig config)
        {
            return new WebDavClient(new WebDavClientParams
            {
                BaseAddress = new Uri(config.WebDavUrl),
                Credentials = new System.Net.NetworkCredential(config.UserName, config.Password)
            });
        }

        public async Task SaveAsync(IEnumerable<FolderItem> folders, IEnumerable<PromptItem> files, CancellationToken cancellationToken = default)
        {
            var config = ConfigService.Load();
            if (string.IsNullOrEmpty(config.UserName) || string.IsNullOrEmpty(config.Password))
            {
                throw new InvalidOperationException("请先在设置中填写 WebDAV 账号和密码！");
            }

            var client = GetClient(config);
            string remotePath = $"{config.RemoteFolderName}/PromptMasterv6Data.json";

            var data = new AppData
            {
                Folders = new List<FolderItem>(folders),
                Files = new List<PromptItem>(files),
                ApiProfiles = new List<ApiProfile>(),
                SavedModels = new List<AiModelConfig>()
            };

            using var contentStream = new MemoryStream();
            await JsonSerializer.SerializeAsync(contentStream, data, _jsonOptions, cancellationToken).ConfigureAwait(false);
            contentStream.Position = 0;

            var response = await client.PutFile(remotePath, contentStream).ConfigureAwait(false);

            if (!response.IsSuccessful)
            {
                throw new InvalidOperationException($"WebDAV 错误 {response.StatusCode}: {response.Description}");
            }
        }

        public async Task<AppData> LoadAsync(CancellationToken cancellationToken = default)
        {
            var config = ConfigService.Load();
            if (string.IsNullOrEmpty(config.UserName) || string.IsNullOrEmpty(config.Password))
            {
                return new AppData();
            }

            var client = GetClient(config);
            string remotePath = $"{config.RemoteFolderName}/PromptMasterv6Data.json";

            try
            {
                var response = await client.GetRawFile(remotePath).ConfigureAwait(false);
                
                if (!response.IsSuccessful || response.Stream == null)
                    return new AppData();

                using (response.Stream)
                {
                    var data = await JsonSerializer.DeserializeAsync<AppData>(response.Stream, _jsonOptions, cancellationToken).ConfigureAwait(false);
                    return data ?? new AppData();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"WebDav LoadAsync Failed: {ex.Message}", "WebDavDataService");
                return new AppData();
            }
        }
    }
}

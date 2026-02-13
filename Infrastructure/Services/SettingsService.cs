using PromptMasterv5.Core.Interfaces;
using PromptMasterv5.Core.Models;
using PromptMasterv5.Infrastructure.Services;
using System.IO.Compression;

namespace PromptMasterv5.Infrastructure.Services
{
    /// <summary>
    /// 配置管理服务实现
    /// 作为配置的单一真实来源，所有 ViewModel 通过此服务访问配置
    /// </summary>
    public class SettingsService : ISettingsService
    {
        public AppConfig Config { get; private set; }
        public LocalSettings LocalConfig { get; private set; }

        public SettingsService()
        {
            // 启动时加载配置
            Config = ConfigService.Load();
            LocalConfig = LocalConfigService.Load();

            InitializeDefaultWebTargets();

            LoggerService.Instance.LogInfo("Settings loaded successfully", "SettingsService.ctor");
        }

        private void InitializeDefaultWebTargets()
        {
            if (Config.WebDirectTargets == null) Config.WebDirectTargets = new();

            void AddIfMissing(string name, string url, string icon)
            {
                if (!Config.WebDirectTargets.Any(t => t.Name.Equals(name, System.StringComparison.OrdinalIgnoreCase)))
                {
                    Config.WebDirectTargets.Add(new WebTarget { Name = name, UrlTemplate = url, IconData = icon });
                }
            }

            // 1. ChatGPT
            AddIfMissing("ChatGPT", "https://chat.openai.com/?q={0}", 
                "M12,2L20.66,7V17L12,22L3.34,17V7L12,2Z");

            // 2. Claude
            AddIfMissing("Claude", "https://claude.ai/new?q={0}", 
                "M12,2A10,10 0 1,0 22,12A10,10 0 0,0 12,2M17,15.5L15.5,17A8,8 0 1,1 15.5,7L17,8.5A6,6 0 1,0 17,15.5Z");

            // 3. Gemini (Google)
            AddIfMissing("Gemini", "https://gemini.google.com/app?q={0}", 
                "M12,2L14.5,9.5L22,12L14.5,14.5L12,22L9.5,14.5L2,12L9.5,9.5Z");

            // 4. Perplexity
            AddIfMissing("Perplexity", "https://www.perplexity.ai/?q={0}", 
                "M12,2V6M12,18V22M4.93,4.93L7.76,7.76M16.24,16.24L19.07,19.07M2,12H6M18,12H22M4.93,19.07L7.76,16.24M16.24,7.76L19.07,4.93");

            // 5. DeepSeek (深度求索)
            AddIfMissing("DeepSeek", "https://chat.deepseek.com?q={0}", 
                "M12,2C6.48,2 2,6.48 2,12s4.48,10 10,10 10-4.48 10-10S17.52,2 12,2zm0,18c-4.41,0-8-3.59-8-8s3.59-8 8-8 8,3.59 8,8-3.59,8-8,8z M12,6c-3.31,0-6,2.69-6,6s2.69,6 6,6 6-2.69 6-6-2.69-6-6-6z");

            // 6. GLM (智谱清言)
            AddIfMissing("GLM", "https://chatglm.cn/main/all?q={0}", 
                "M20,2H4C2.9,2 2,2.9 2,4V22L6,18H20C21.1,18 22,17.1 22,16V4C22,2.9 21.1,2 20,2M20,16H5.17L4,17.17V4H20V16Z");

            // 7. Qwen (通义千问)
            AddIfMissing("Qwen", "https://tongyi.aliyun.com/qianwen?q={0}", 
                "M12,2A10,10 0 0,1 22,12C22,14.25 21.17,16.31 19.82,17.85L22.61,20.64L21.2,22.05L18.41,19.26C16.97,20.34 15.1,21 13,21A10,10 0 0,1 2,12A10,10 0 0,1 12,2M12,4A8,8 0 0,0 4,12A8,8 0 0,0 12,20A8,8,0,0,0,20,12A8,8,0,0,0,12,4Z");

            // 8. Doubao (豆包)
            AddIfMissing("Doubao", "https://www.doubao.com/chat/?q={0}", 
                "M12,2C6.48,2 2,6.48 2,12s4.48,10 10,10 10-4.48 10-10S17.52,2 12,2zm0,18c-4.41,0-8-3.59-8-8s3.59-8 8-8 8,3.59 8,8-3.59,8-8,8z M12,8c-2.21,0-4,1.79-4,4s1.79,4 4,4 4-1.79 4-4-1.79-4-4-4z");

            // 9. AI Studio (Google)
            AddIfMissing("AI Studio", "https://aistudio.google.com/prompts/new_chat?q={0}", 
                "M12,2L14.5,9.5L22,12L14.5,14.5L12,22L9.5,14.5L2,12L9.5,9.5Z M12,8L13,10.5L15.5,11.5L13,12.5L12,15L11,12.5L8.5,11.5L11,10.5Z");

            // Save updates
            SaveConfig();

            // Migration: Ensure all targets have ?q={0} (for Userscript support)
            bool needsSave = false;
            foreach (var target in Config.WebDirectTargets)
            {
                // List of targets that should have query params now
                var scriptTargets = new[] { "Gemini", "DeepSeek", "GLM", "Qwen", "Doubao", "AI Studio" };
                
                if (scriptTargets.Contains(target.Name, StringComparer.OrdinalIgnoreCase) && !target.UrlTemplate.Contains("{0}"))
                {
                    if (target.Name == "Gemini") target.UrlTemplate = "https://gemini.google.com/app?q={0}";
                    if (target.Name == "DeepSeek") target.UrlTemplate = "https://chat.deepseek.com?q={0}";
                    if (target.Name == "GLM") target.UrlTemplate = "https://chatglm.cn/main/all?q={0}";
                    if (target.Name == "Qwen") target.UrlTemplate = "https://tongyi.aliyun.com/qianwen?q={0}";
                    if (target.Name == "Doubao") target.UrlTemplate = "https://www.doubao.com/chat/?q={0}";
                    if (target.Name == "AI Studio") target.UrlTemplate = "https://aistudio.google.com/prompts/new_chat?q={0}";
                    needsSave = true;
                }
            }

            if (needsSave) SaveConfig();
        }

        public void SaveConfig()
        {
            try
            {
                ConfigService.Save(Config);
                LoggerService.Instance.LogInfo("AppConfig saved", "SettingsService.SaveConfig");
            }
            catch (System.Exception ex)
            {
                LoggerService.Instance.LogException(ex, "Failed to save AppConfig", "SettingsService.SaveConfig");
                throw;
            }
        }

        public void SaveLocalConfig()
        {
            try
            {
                LocalConfigService.Save(LocalConfig);
                LoggerService.Instance.LogInfo("LocalSettings saved", "SettingsService.SaveLocalConfig");
            }
            catch (System.Exception ex)
            {
                LoggerService.Instance.LogException(ex, "Failed to save LocalSettings", "SettingsService.SaveLocalConfig");
                throw;
            }
        }

        public void ReloadConfigs()
        {
            try
            {
                Config = ConfigService.Load();
                LocalConfig = LocalConfigService.Load();
                LoggerService.Instance.LogInfo("Settings reloaded", "SettingsService.ReloadConfigs");
            }
            catch (System.Exception ex)
            {
                LoggerService.Instance.LogException(ex, "Failed to reload settings", "SettingsService.ReloadConfigs");
                throw;
            }
        }

        public void ExportSettings(string zipPath)
        {
            try
            {
                // 先保存当前内存中的配置到磁盘，确保导出的是最新状态
                SaveConfig();
                SaveLocalConfig();

                if (System.IO.File.Exists(zipPath))
                {
                    System.IO.File.Delete(zipPath);
                }

                // 创建临时目录来存放要打包的文件
                // 虽然可以直接从ConfigPath打包，但为了扩展性和安全性，显式指定要打包的文件更稳妥
                // 这里我们直接利用 System.IO.Compression.ZipFile 的 CreateFromDirectory 或者逐个添加
                // 由于只打包两个特定文件，手动创建 zip 更灵活

                using (var archive = System.IO.Compression.ZipFile.Open(zipPath, System.IO.Compression.ZipArchiveMode.Create))
                {
                    string configPath = System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "config.json");
                    string localConfigPath = System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "local_settings.json");

                    if (System.IO.File.Exists(configPath))
                    {
                        archive.CreateEntryFromFile(configPath, "config.json");
                    }

                    if (System.IO.File.Exists(localConfigPath))
                    {
                        archive.CreateEntryFromFile(localConfigPath, "local_settings.json");
                    }
                }

                LoggerService.Instance.LogInfo($"Settings exported to {zipPath}", "SettingsService.ExportSettings");
            }
            catch (System.Exception ex)
            {
                LoggerService.Instance.LogException(ex, "Failed to export settings", "SettingsService.ExportSettings");
                throw;
            }
        }

        public void ImportSettings(string zipPath)
        {
            try
            {
                if (!System.IO.File.Exists(zipPath))
                {
                    throw new System.IO.FileNotFoundException("Import file not found", zipPath);
                }

                using (var archive = System.IO.Compression.ZipFile.OpenRead(zipPath))
                {
                    var configEntry = archive.GetEntry("config.json");
                    var localConfigEntry = archive.GetEntry("local_settings.json");

                    if (configEntry == null && localConfigEntry == null)
                    {
                        throw new System.Exception("The selected file does not contain valid configuration files (config.json or local_settings.json).");
                    }

                    string baseDir = System.AppDomain.CurrentDomain.BaseDirectory;

                    if (configEntry != null)
                    {
                        string targetPath = System.IO.Path.Combine(baseDir, "config.json");
                        configEntry.ExtractToFile(targetPath, overwrite: true);
                    }

                    if (localConfigEntry != null)
                    {
                        string targetPath = System.IO.Path.Combine(baseDir, "local_settings.json");
                        localConfigEntry.ExtractToFile(targetPath, overwrite: true);
                    }
                }

                // 导入后重新加载内存中的配置
                ReloadConfigs();

                LoggerService.Instance.LogInfo($"Settings imported from {zipPath}", "SettingsService.ImportSettings");
            }
            catch (System.Exception ex)
            {
                LoggerService.Instance.LogException(ex, "Failed to import settings", "SettingsService.ImportSettings");
                throw;
            }
        }
    }
}

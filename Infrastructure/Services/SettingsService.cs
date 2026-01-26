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

            LoggerService.Instance.LogInfo("Settings loaded successfully", "SettingsService.ctor");
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

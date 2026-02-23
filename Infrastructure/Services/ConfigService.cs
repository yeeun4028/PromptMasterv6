using System;
using System.IO;
using System.Text.Json;
using PromptMasterv5.Core.Models;

namespace PromptMasterv5.Infrastructure.Services
{
    public static class ConfigService
    {
        private static readonly string ConfigPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");

        public static void Save(AppConfig config)
        {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(config, options);
                File.WriteAllText(ConfigPath, json);
            }
            catch (Exception ex)
            {
                LoggerService.Instance.LogException(ex, $"Failed to save config to {ConfigPath}", "ConfigService.Save");
            }
        }

        public static AppConfig Load()
        {
            if (!File.Exists(ConfigPath)) return new AppConfig();
            try
            {
                string json = File.ReadAllText(ConfigPath);
                var config = JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();

                if (string.IsNullOrWhiteSpace(config.FullWindowHotkey) && !string.IsNullOrWhiteSpace(config.GlobalHotkey))
                {
                    config.FullWindowHotkey = config.GlobalHotkey;
                }


                return config;
            }
            catch
            {
                return new AppConfig();
            }
        }
    }
}

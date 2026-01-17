using System;
using System.IO;
using System.Text.Json;
using PromptMasterv5.Models;

namespace PromptMasterv5.Services
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
            catch { /* 忽略保存错误 */ }
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

                if (string.IsNullOrWhiteSpace(config.MiniWindowHotkey) && !string.IsNullOrWhiteSpace(config.SingleHotkey))
                {
                    config.MiniWindowHotkey = config.SingleHotkey;
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

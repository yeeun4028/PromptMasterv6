using System;
using System.IO;
using System.Text.Json;
using PromptMasterv5.Models;

namespace PromptMasterv5.Services
{
    public static class LocalConfigService
    {
        // 文件名不同，避免冲突
        private static readonly string ConfigPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "local_settings.json");

        public static void Save(LocalSettings settings)
        {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(settings, options);
                File.WriteAllText(ConfigPath, json);
            }
            catch { /* 忽略保存错误 */ }
        }

        public static LocalSettings Load()
        {
            if (!File.Exists(ConfigPath)) return new LocalSettings();
            try
            {
                string json = File.ReadAllText(ConfigPath);
                return JsonSerializer.Deserialize<LocalSettings>(json) ?? new LocalSettings();
            }
            catch
            {
                return new LocalSettings();
            }
        }
    }
}
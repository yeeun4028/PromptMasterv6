using System;
using System.IO;
using System.Text.Json;
using PromptMasterv6.Core.Models;

namespace PromptMasterv6.Infrastructure.Services
{
    public static class LocalConfigService
    {
        private static readonly string ConfigPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "local_settings.json");

        public static void Save(LocalSettings settings)
        {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(settings, options);
                File.WriteAllText(ConfigPath, json);
            }
            catch (Exception ex)
            {
                LoggerService.Instance.LogException(ex, $"Failed to save local settings to {ConfigPath}", "LocalConfigService.Save");
            }
        }

        public static LocalSettings Load()
        {
            if (!File.Exists(ConfigPath)) return new LocalSettings();
            try
            {
                string json = File.ReadAllText(ConfigPath);
                var settings = JsonSerializer.Deserialize<LocalSettings>(json) ?? new LocalSettings();

                // 兼容旧配置：补全 ActionIcons 中可能缺失的 key
                var defaultKeys = new[] { "CreateFile", "CreateFolder", "Import", "Settings" };
                foreach (var key in defaultKeys)
                    settings.ActionIcons.TryAdd(key, "");

                return settings;
            }
            catch
            {
                return new LocalSettings();
            }
        }
    }
}

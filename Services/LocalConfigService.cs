using System;
using System.IO;
using System.Text.Json;
using PromptMasterv5.Core.Models;

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
                var settings = JsonSerializer.Deserialize<LocalSettings>(json) ?? new LocalSettings();

                if (settings.CoordinateRules == null)
                {
                    settings.CoordinateRules = new();
                }

                if (settings.CoordinateRules.Count == 0)
                {
                    settings.CoordinateRules.Add(new CoordinateRule
                    {
                        X = settings.ClickX,
                        Y = settings.ClickY,
                        UrlContains = ""
                    });
                }

                if (settings.MiniPinnedPromptIds == null)
                {
                    settings.MiniPinnedPromptIds = new();
                }

                if (settings.MiniPinnedPromptIds.Count == 0 && !string.IsNullOrWhiteSpace(settings.MiniPinnedPromptId))
                {
                    settings.MiniPinnedPromptIds.Add(settings.MiniPinnedPromptId);
                }

                return settings;
            }
            catch
            {
                return new LocalSettings();
            }
        }
    }
}

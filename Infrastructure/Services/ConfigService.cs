using System;
using System.IO;
using System.Text.Json;

namespace PromptMasterv6.Infrastructure.Services
{
    public static class ConfigService
    {
        private static readonly string ConfigPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");

        /// <summary>
        /// 共享的序列化选项：启用 AllowNamedFloatingPointLiterals 以支持 NaN/Infinity。
        /// NaN/Infinity 永远不应出现在 AppConfig 中（Sanitize() 负责拦截），
        /// 但此选项作为最后保障，防止极端情况导致崩溃。
        /// </summary>
        private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals
        };

        public static void Save(AppConfig config)
        {
            try
            {
                string json = JsonSerializer.Serialize(config, _jsonOptions);
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
                var config = JsonSerializer.Deserialize<AppConfig>(json, _jsonOptions) ?? new AppConfig();

                if (string.IsNullOrWhiteSpace(config.FullWindowHotkey) && !string.IsNullOrWhiteSpace(config.GlobalHotkey))
                {
                    config.FullWindowHotkey = config.GlobalHotkey;
                }

                // 净化所有 double 字段，将历史遗留的 Infinity 值替换为安全默认值
                // 防止损坏的 config.json 导致 ConfigService.Save 在启动时反复崩溃
                config.Sanitize();

                return config;
            }
            catch
            {
                return new AppConfig();
            }
        }
    }
}

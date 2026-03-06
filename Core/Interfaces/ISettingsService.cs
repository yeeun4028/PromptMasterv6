using PromptMasterv6.Core.Models;

namespace PromptMasterv6.Core.Interfaces
{
    /// <summary>
    /// 提供应用程序配置管理服务
    /// 所有 ViewModel 通过此服务访问和修改配置，确保配置的一致性
    /// </summary>
    public interface ISettingsService
    {
        /// <summary>
        /// 获取应用程序全局配置（API Key、模型等）
        /// </summary>
        AppConfig Config { get; }

        /// <summary>
        /// 获取本地用户配置（UI 状态、偏好设置等）
        /// </summary>
        LocalSettings LocalConfig { get; }

        /// <summary>
        /// 保存全局配置到磁盘
        /// </summary>
        void SaveConfig();

        /// <summary>
        /// 保存本地配置到磁盘
        /// </summary>
        void SaveLocalConfig();

        /// <summary>
        /// 重新加载配置（用于从云端恢复后刷新）
        /// </summary>
        void ReloadConfigs();

        /// <summary>
        /// 导出当前配置（config.json 和 local_settings.json）到指定的 zip 文件
        /// </summary>
        /// <param name="zipPath">目标 zip 文件路径</param>
        void ExportSettings(string zipPath);

        /// <summary>
        /// 从指定的 zip 文件导入配置（config.json 和 local_settings.json）
        /// 并重新加载内存中的配置
        /// </summary>
        /// <param name="zipPath">源 zip 文件路径</param>
        void ImportSettings(string zipPath);
    }
}

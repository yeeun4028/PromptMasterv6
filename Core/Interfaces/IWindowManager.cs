using System;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using PromptMasterv6.Core.Models;

namespace PromptMasterv6.Core.Interfaces
{
    public interface IWindowManager
    {
        Task<byte[]?> ShowCaptureWindowAsync(Func<byte[], System.Windows.Rect, Task>? onCaptureProcessing = null);
        void ShowTranslationPopup(string text, System.Windows.Rect? placementTarget = null);
        
        void CloseWindow(object viewModel);
        void ShowLauncherWindow();
        void ShowSettingsWindow(object viewModel);
        void CloseSettingsWindow();

        /// <summary>
        /// 显示贴图窗口（从截图）
        /// </summary>
        /// <param name="options">贴图配置选项</param>
        /// <returns>异步任务</returns>
        Task ShowPinToScreenFromCaptureAsync(PinToScreenOptions? options = null);

        /// <summary>
        /// 显示贴图窗口（从剪贴板图片）
        /// </summary>
        /// <param name="options">贴图配置选项</param>
        /// <returns>是否成功</returns>
        bool ShowPinToScreenFromClipboard(PinToScreenOptions? options = null);

        /// <summary>
        /// 显示贴图窗口（从图片文件）
        /// </summary>
        /// <param name="filePath">图片文件路径</param>
        /// <param name="options">贴图配置选项</param>
        /// <returns>是否成功</returns>
        bool ShowPinToScreenFromFile(string filePath, PinToScreenOptions? options = null);

        /// <summary>
        /// 显示贴图窗口（从 BitmapSource）
        /// </summary>
        /// <param name="image">图片源</param>
        /// <param name="options">贴图配置选项</param>
        /// <param name="location">显示位置</param>
        void ShowPinToScreen(BitmapSource image, PinToScreenOptions? options = null, System.Windows.Point? location = null);

        /// <summary>
        /// 关闭所有贴图窗口
        /// </summary>
        void CloseAllPinToScreenWindows();

        /// <summary>
        /// 获取当前打开的贴图窗口数量
        /// </summary>
        int GetPinToScreenWindowCount();
    }
}

using MediaColor = System.Windows.Media.Color;
using MediaColors = System.Windows.Media.Colors;

namespace PromptMasterv5.Core.Models
{
    /// <summary>
    /// 贴图（钉图）功能的配置选项
    /// </summary>
    public class PinToScreenOptions
    {
        /// <summary>
        /// 初始缩放比例 (20-500)
        /// </summary>
        public int InitialScale { get; set; } = 100;

        /// <summary>
        /// 缩放步进值
        /// </summary>
        public int ScaleStep { get; set; } = 10;

        /// <summary>
        /// 是否使用高质量缩放
        /// </summary>
        public bool HighQualityScale { get; set; } = true;

        /// <summary>
        /// 初始透明度 (10-100)
        /// </summary>
        public int InitialOpacity { get; set; } = 100;

        /// <summary>
        /// 透明度步进值
        /// </summary>
        public int OpacityStep { get; set; } = 10;

        /// <summary>
        /// 是否始终置顶
        /// </summary>
        public bool TopMost { get; set; } = true;

        /// <summary>
        /// 缩放时是否保持中心位置
        /// </summary>
        public bool KeepCenterLocation { get; set; } = true;

        /// <summary>
        /// 背景颜色
        /// </summary>
        public MediaColor BackgroundColor { get; set; } = MediaColors.Transparent;

        /// <summary>
        /// 是否显示窗口阴影
        /// </summary>
        public bool Shadow { get; set; } = true;

        /// <summary>
        /// 是否显示边框
        /// </summary>
        public bool Border { get; set; } = true;

        /// <summary>
        /// 边框宽度
        /// </summary>
        public int BorderSize { get; set; } = 2;

        /// <summary>
        /// 边框颜色
        /// </summary>
        public MediaColor BorderColor { get; set; } = MediaColor.FromRgb(79, 148, 212); // CornflowerBlue

        /// <summary>
        /// 最小化时的尺寸
        /// </summary>
        public System.Windows.Size MinimizeSize { get; set; } = new System.Windows.Size(100, 100);
    }
}

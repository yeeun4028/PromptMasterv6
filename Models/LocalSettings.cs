using CommunityToolkit.Mvvm.ComponentModel;

namespace PromptMasterv5.Models
{
    public enum InputMode
    {
        SmartFocus = 0, // 智能回退
        CoordinateClick = 1 // 坐标点击
    }

    public enum ThemeType
    {
        Light = 0,
        Dark = 1
    }

    public partial class LocalSettings : ObservableObject
    {
        [ObservableProperty]
        private InputMode mode = InputMode.SmartFocus;

        [ObservableProperty]
        private int clickX = 0;

        [ObservableProperty]
        private int clickY = 0;

        // --- 新增配置项 ---

        [ObservableProperty]
        private ThemeType theme = ThemeType.Dark; // 默认深色，符合极简模式定位

        [ObservableProperty]
        private double miniFontSize = 18.0;

        // 极简模式窗口位置记忆
        public double MiniWindowTop { get; set; } = 100;
        public double MiniWindowLeft { get; set; } = 100;

        // 完整模式窗口位置记忆
        public double FullWindowTop { get; set; } = 100;
        public double FullWindowLeft { get; set; } = 100;
        public double FullWindowWidth { get; set; } = 1000;
        public double FullWindowHeight { get; set; } = 600;
    }
}
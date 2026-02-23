using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using System.Text.Json.Serialization;

namespace PromptMasterv5.Core.Models
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

        [ObservableProperty]
        private ObservableCollection<CoordinateRule> coordinateRules = new() { new CoordinateRule() };

        // --- 新增配置项 ---

        [ObservableProperty]
        private ThemeType theme = ThemeType.Light;

        [ObservableProperty] 
        private string ocrHotkey = ""; // OCR 快捷键（从外部工具设置页面配置）

        [ObservableProperty] 
        private string translateHotkey = ""; // 翻译快捷键（从外部工具设置页面配置） 

        // 完整模式窗口位置记忆
        public double FullWindowTop { get; set; } = 100;
        public double FullWindowLeft { get; set; } = 100;
        public double FullWindowWidth { get; set; } = 1000;

        public double FullWindowHeight { get; set; } = 600;

        // 侧边栏和列表栏宽度记忆
        [ObservableProperty]
        private double block1Width = 60;

        [ObservableProperty]
        private double block2Width = 250;

        // 存储按钮自定义图标（SVG Path Data）
        // Key: 按钮标识符（"CreateFile", "CreateFolder", "Import", "Settings"）
        // Value: SVG Path Data 字符串
        public Dictionary<string, string> ActionIcons { get; set; } = new();

        // Quick Action prompts for 划词 feature
        [ObservableProperty]
        private ObservableCollection<QuickActionPrompt> quickActionPrompts = new();

        [ObservableProperty]
        private DateTime? lastCloudSyncTime;
    }

    // Model for Quick Action prompts with individual model selection
    public partial class QuickActionPrompt : ObservableObject
    {
        [ObservableProperty]
        private string id = "";

        [ObservableProperty]
        private string title = "";

        [ObservableProperty]
        private string boundModelId = ""; // Empty = use global default model
    }
}

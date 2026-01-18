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
        private double miniFontSize = 18.0;

        [ObservableProperty]
        private bool miniWindowUseAi = false; // 极简窗口默认使用AI

        [ObservableProperty]
        private bool miniEnterForAi = false; // Enter键用于AI查询（true=AI, false=普通发送）

        [ObservableProperty]
        private bool miniAiOnlyChatEnabled = false;

        [ObservableProperty]
        private string miniPatternPrefix = "ai";

        [ObservableProperty] 
 private string miniAlwaysOnTopHotkeyPrefix = ""; 

[ObservableProperty] 
 private string ocrHotkey = "F9"; // 默认 F9 触发 OCR 

[ObservableProperty] 
 private string translateHotkey = "Alt+D"; // 默认 Alt+D 触发翻译 

[ObservableProperty] 
 [property: JsonIgnore] 
 private bool isMiniTopmostLocked = false;

        // 极简模式窗口位置记忆
        public double MiniWindowTop { get; set; } = 100;
        public double MiniWindowLeft { get; set; } = 100;
        public double MiniWindowWidth { get; set; } = 500;
        public double MiniWindowHeight { get; set; } = 0;

        public bool MiniUseDefaultPosition { get; set; } = false;

        [ObservableProperty]
        private double miniDefaultLeft = 100;

        [ObservableProperty]
        private double miniDefaultBottom = 600;

        public double MiniDefaultWidth { get; set; } = 500;
        public double MiniDefaultHeight { get; set; } = 0;
        public double MiniExpandedWidth { get; set; } = 800;
        public bool MiniClearAiResultOnTyping { get; set; } = true;

        public string MiniPinnedPromptId { get; set; } = "";
        public string MiniPinnedPromptCandidateId { get; set; } = "";
        public ObservableCollection<string> MiniPinnedPromptIds { get; set; } = new();
        [JsonIgnore]
        public string MiniSelectedPinnedPromptId { get; set; } = "";
        public bool MiniPinnedPromptShowIcons { get; set; } = false;
        public bool MiniPinnedPromptClickShowsFullContent { get; set; } = true;

        // 完整模式窗口位置记忆
        public double FullWindowTop { get; set; } = 100;
        public double FullWindowLeft { get; set; } = 100;
        public double FullWindowWidth { get; set; } = 1000;
        public double FullWindowHeight { get; set; } = 600;

        // 存储按钮自定义图标（SVG Path Data）
        // Key: 按钮标识符（"CreateFile", "CreateFolder", "Import", "Settings"）
        // Value: SVG Path Data 字符串
        public Dictionary<string, string> ActionIcons { get; set; } = new();
    }
}

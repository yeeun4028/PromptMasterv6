using CommunityToolkit.Mvvm.ComponentModel;

namespace PromptMasterv5.Models
{
    public enum InputMode
    {
        SmartFocus = 0, // 智能回退：回到上一个激活的窗口
        CoordinateClick = 1 // 坐标点击：强制点击指定位置
    }

    public partial class LocalSettings : ObservableObject
    {
        [ObservableProperty]
        private InputMode mode = InputMode.SmartFocus;

        [ObservableProperty]
        private int clickX = 0;

        [ObservableProperty]
        private int clickY = 0;
    }
}
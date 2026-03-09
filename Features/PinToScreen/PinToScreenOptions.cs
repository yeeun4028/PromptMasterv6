using MediaColor = System.Windows.Media.Color;
using MediaColors = System.Windows.Media.Colors;

namespace PromptMasterv6.Features.PinToScreen;

public class PinToScreenOptions
{
    public int InitialScale { get; set; } = 100;
    public int ScaleStep { get; set; } = 10;
    public bool HighQualityScale { get; set; } = true;
    public int InitialOpacity { get; set; } = 100;
    public int OpacityStep { get; set; } = 10;
    public bool TopMost { get; set; } = true;
    public bool KeepCenterLocation { get; set; } = true;
    public MediaColor BackgroundColor { get; set; } = MediaColors.Transparent;
    public bool Shadow { get; set; } = true;
    public bool Border { get; set; } = true;
    public int BorderSize { get; set; } = 2;
    public MediaColor BorderColor { get; set; } = MediaColor.FromRgb(79, 148, 212);
    public System.Windows.Size MinimizeSize { get; set; } = new System.Windows.Size(100, 100);
}

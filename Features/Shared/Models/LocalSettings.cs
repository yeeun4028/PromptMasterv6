using CommunityToolkit.Mvvm.ComponentModel;

namespace PromptMasterv6.Features.Shared.Models;

public partial class LocalSettings : ObservableObject
{
    [ObservableProperty] 
    private string ocrHotkey = "";

    [ObservableProperty] 
    private string translateHotkey = "";

    public double FullWindowTop { get; set; } = 100;
    public double FullWindowLeft { get; set; } = 100;
    public double FullWindowWidth { get; set; } = 1000;
    public double FullWindowHeight { get; set; } = 600;

    [ObservableProperty]
    private double block1Width = 60;

    [ObservableProperty]
    private double block2Width = 250;

    public Dictionary<string, string> ActionIcons { get; set; } = new()
    {
        ["CreateFile"]   = "",
        ["CreateFolder"] = "",
        ["Import"]       = "",
        ["Settings"]     = "",
    };

    [ObservableProperty]
    private DateTime? lastCloudSyncTime;
}

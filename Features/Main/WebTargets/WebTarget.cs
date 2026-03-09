using CommunityToolkit.Mvvm.ComponentModel;

namespace PromptMasterv6.Features.Main.WebTargets;

public partial class WebTarget : ObservableObject
{
    [ObservableProperty]
    private string name = "";

    [ObservableProperty]
    private string urlTemplate = "";

    [ObservableProperty]
    private string iconData = "";

    [ObservableProperty]
    private bool isEnabled = true;
}

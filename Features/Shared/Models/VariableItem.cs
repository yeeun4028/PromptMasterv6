using CommunityToolkit.Mvvm.ComponentModel;

namespace PromptMasterv6.Features.Shared.Models;

public partial class VariableItem : ObservableObject
{
    [ObservableProperty]
    private string name = "";

    [ObservableProperty]
    private string value = "";
}

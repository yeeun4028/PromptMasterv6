using CommunityToolkit.Mvvm.ComponentModel;

namespace PromptMasterv5.Core.Models
{
    public partial class WebTarget : ObservableObject
    {
        [ObservableProperty]
        private string name = "";

        [ObservableProperty]
        private string urlTemplate = "";

        [ObservableProperty]
        private string iconData = ""; // SVG Geometry String

        [ObservableProperty]
        private bool isEnabled = true;
    }
}

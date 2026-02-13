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

        partial void OnIsEnabledChanged(bool value)
        {
            // Optional: Trigger save or update logic if needed, but binding to Config should verify auto-save if implemented
        }
    }
}

using CommunityToolkit.Mvvm.ComponentModel;

namespace PromptMasterv5.Models
{
    public partial class CoordinateRule : ObservableObject
    {
        [ObservableProperty]
        private int x = 0;

        [ObservableProperty]
        private int y = 0;

        [ObservableProperty]
        private string urlContains = "";
    }
}


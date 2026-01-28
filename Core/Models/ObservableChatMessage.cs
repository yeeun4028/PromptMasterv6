using CommunityToolkit.Mvvm.ComponentModel;

namespace PromptMasterv5.Core.Models
{
    public partial class ObservableChatMessage : ObservableObject
    {
        [ObservableProperty]
        private string role;

        [ObservableProperty]
        private string content;

        public ObservableChatMessage(string role, string content)
        {
            Role = role;
            Content = content;
        }
    }
}

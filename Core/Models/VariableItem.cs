using CommunityToolkit.Mvvm.ComponentModel;

namespace PromptMasterv6.Core.Models
{
    public partial class VariableItem : ObservableObject
    {
        [ObservableProperty]
        private string name = ""; // 给默认值

        [ObservableProperty]
        private string value = ""; // 给默认值
    }
}
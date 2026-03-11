using System.Windows;
using System.Windows.Controls;

namespace PromptMasterv6.Features.Settings
{
    public partial class SettingsView : System.Windows.Controls.UserControl
    {
        public SettingsView()
        {
            InitializeComponent();
            Loaded += SettingsView_Loaded;
        }

        private void SettingsView_Loaded(object sender, RoutedEventArgs e)
        {
        }

        private SettingsViewModel? ViewModel => DataContext as SettingsViewModel;
    }
}

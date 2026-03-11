using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using PromptMasterv6.Infrastructure.Services;

namespace PromptMasterv6.Features.Settings.Sync
{
    public partial class SyncView : System.Windows.Controls.UserControl
    {
        public SyncView()
        {
            InitializeComponent();
        }

        private void SyncSubTab_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn && btn.Tag != null)
            {
                var tag = btn.Tag.ToString();

                SyncWebDavTab.Visibility = tag == "Selected" ? Visibility.Visible : Visibility.Collapsed;
                SyncDataTab.Visibility = tag == "1" ? Visibility.Visible : Visibility.Collapsed;
                SyncLogTab.Visibility = tag == "2" ? Visibility.Visible : Visibility.Collapsed;

                BtnSyncWebDavTab.Tag = tag == "Selected" ? "Selected" : "0";
                BtnSyncDataTab.Tag = tag == "1" ? "Selected" : "1";
                BtnSyncLogTab.Tag = tag == "2" ? "Selected" : "2";
            }
        }

        private void WebDavPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (DataContext is SyncViewModel vm)
            {
                vm.Config.Password = ((PasswordBox)sender).Password;
            }
        }

        private void WebDavPasswordBox_Loaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is SyncViewModel vm)
            {
                ((PasswordBox)sender).Password = vm.Config.Password;
            }
        }
    }
}

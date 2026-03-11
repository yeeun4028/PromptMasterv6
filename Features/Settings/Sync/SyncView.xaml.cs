using System.Windows;
using System.Windows.Controls;
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
            if (sender is System.Windows.Controls.Button btn)
            {
                SyncWebDAVTab.Visibility = btn == BtnSyncWebDavTab ? Visibility.Visible : Visibility.Collapsed;
                SyncDataTab.Visibility = btn == BtnSyncDataTab ? Visibility.Visible : Visibility.Collapsed;
                SyncLogTab.Visibility = btn == BtnSyncLogTab ? Visibility.Visible : Visibility.Collapsed;

                BtnSyncWebDavTab.Tag = btn == BtnSyncWebDavTab ? "Selected" : "0";
                BtnSyncDataTab.Tag = btn == BtnSyncDataTab ? "Selected" : "1";
                BtnSyncLogTab.Tag = btn == BtnSyncLogTab ? "Selected" : "2";
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

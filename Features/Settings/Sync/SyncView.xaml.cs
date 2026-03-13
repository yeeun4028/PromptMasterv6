using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using PromptMasterv6.Infrastructure.Services;

namespace PromptMasterv6.Features.Settings.Sync
{
    /// <summary>
    /// VSA 重构后的 SyncView - 支持默认构造函数和 DI 注入
    /// </summary>
    public partial class SyncView : System.Windows.Controls.UserControl
    {
        // 默认构造函数 - 供 XAML 使用
        public SyncView()
        {
            var app = System.Windows.Application.Current as App;
            if (app?.ServiceProvider != null)
            {
                DataContext = app.ServiceProvider.GetService(typeof(SyncViewModel));
            }
            InitializeComponent();
        }

        // DI 构造函数 - 供 DI 容器使用
        public SyncView(SyncViewModel viewModel)
        {
            DataContext = viewModel;
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

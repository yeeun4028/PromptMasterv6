using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.Messaging;

namespace PromptMasterv6.Features.Settings
{
    /// <summary>
    /// VSA 重构后的 SettingsWindow - 通过构造函数注入获取 ViewModel 和 View
    /// </summary>
    public partial class SettingsWindow : System.Windows.Window
    {
        public SettingsWindow(SettingsViewModel viewModel, SettingsView settingsView)
        {
            InitializeComponent();

            // 设置窗口的 DataContext
            DataContext = viewModel;

            // 将 DI 容器创建的 SettingsView 添加到 Grid 中
            RootGrid.Children.Add(settingsView);
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                this.DragMove();
            }
        }

        private void Grid_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;

            Mouse.Capture(this, CaptureMode.SubTree);
            Mouse.Capture(null);

            Dispatcher.BeginInvoke(() =>
            {
                Close();
            }, DispatcherPriority.ContextIdle);
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (DataContext is SettingsViewModel settingsVM)
            {
                settingsVM.IsSettingsOpen = false;
            }
        }
    }
}

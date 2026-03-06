using System.Windows;
using System.Windows.Input;
using PromptMasterv6.ViewModels;
using System.Windows.Threading;

namespace PromptMasterv6.Views
{
    public partial class SettingsWindow : Window
    {
        public SettingsWindow()
        {
            InitializeComponent();
        }

        public void SetDataContext(MainViewModel viewModel)
        {
            DataContext = viewModel;
            SettingsViewContent.DataContext = viewModel;
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
            
            // 捕获鼠标，阻止事件传播
            Mouse.Capture(this, CaptureMode.SubTree);
            Mouse.Capture(null);
            
            // 延迟关闭，确保事件完全处理
            Dispatcher.BeginInvoke(() =>
            {
                Close();
            }, DispatcherPriority.ContextIdle);
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (DataContext is MainViewModel mainVM)
            {
                mainVM.SettingsVM.IsSettingsOpen = false;
            }
        }
    }
}

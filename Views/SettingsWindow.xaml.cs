using System.Windows;
using System.Windows.Input;
using PromptMasterv6.ViewModels;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.Messaging;
using PromptMasterv6.ViewModels.Messages;

namespace PromptMasterv6.Views
{
    public partial class SettingsWindow : Window
    {
        public SettingsWindow()
        {
            InitializeComponent();
        }

        public void SetDataContext(SettingsViewModel viewModel)
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

using System;
using System.Windows;
using System.Windows.Input;
using PromptMasterv5.ViewModels;

namespace PromptMasterv5.Views
{
    public partial class LauncherWindow : Window
    {
        public LauncherWindow()
        {
            InitializeComponent();
            Closing += (s, e) => _isClosing = true;
        }

        private void Window_Deactivated(object sender, EventArgs e)
        {
            // 如果失去焦点，关闭窗口
            SafeClose();
        }

        private bool _isClosing = false;
        private void SafeClose()
        {
            if (_isClosing) return;
            _isClosing = true;
            try { Close(); } catch { }
        }

        private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                SafeClose();
            }
        }
    }
}

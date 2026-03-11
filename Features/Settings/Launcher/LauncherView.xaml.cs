using System.Windows.Controls;
using System.Windows.Input;
using PromptMasterv6.Infrastructure.Services;

namespace PromptMasterv6.Features.Settings.Launcher
{
    public partial class LauncherView : System.Windows.Controls.UserControl
    {
        public LauncherView()
        {
            InitializeComponent();
        }

        private void LauncherHotkeyTextBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (sender is System.Windows.Controls.TextBox textBox)
            {
                e.Handled = true;

                if (e.Key == Key.Delete || e.Key == Key.Back)
                {
                    textBox.Text = "";
                    UpdateConfigProperty("LauncherHotkey", "");
                    return;
                }

                var modifiers = Keyboard.Modifiers;
                var key = e.Key;

                if (key == Key.LeftCtrl || key == Key.RightCtrl ||
                    key == Key.LeftAlt || key == Key.RightAlt ||
                    key == Key.LeftShift || key == Key.RightShift ||
                    key == Key.LWin || key == Key.RWin)
                {
                    return;
                }

                var hotkeyStr = "";
                if (modifiers.HasFlag(ModifierKeys.Control)) hotkeyStr += "Ctrl+";
                if (modifiers.HasFlag(ModifierKeys.Alt)) hotkeyStr += "Alt+";
                if (modifiers.HasFlag(ModifierKeys.Shift)) hotkeyStr += "Shift+";
                if (modifiers.HasFlag(ModifierKeys.Windows)) hotkeyStr += "Win+";

                hotkeyStr += key.ToString();
                textBox.Text = hotkeyStr;
                UpdateConfigProperty("LauncherHotkey", hotkeyStr);
            }
        }

        private void UpdateConfigProperty(string propertyName, string value)
        {
            if (DataContext is LauncherSettingsViewModel vm)
            {
                var config = vm.Config;
                var prop = config.GetType().GetProperty(propertyName);
                prop?.SetValue(config, value);
            }
        }
    }
}

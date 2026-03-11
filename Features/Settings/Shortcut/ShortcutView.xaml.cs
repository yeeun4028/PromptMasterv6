using System.Windows.Controls;
using System.Windows.Input;
using PromptMasterv6.Infrastructure.Services;

namespace PromptMasterv6.Features.Settings.Shortcut
{
    public partial class ShortcutView : System.Windows.Controls.UserControl
    {
        public ShortcutView()
        {
            InitializeComponent();
        }

        private void FullWindowHotkeyTextBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            HandleHotkeyPreviewKeyDown(sender, e, "FullWindowHotkey");
        }

        private void ScreenshotTranslateHotkeyTextBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            HandleHotkeyPreviewKeyDown(sender, e, "ScreenshotTranslateHotkey");
        }

        private void OcrHotkeyTextBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            HandleHotkeyPreviewKeyDown(sender, e, "OcrHotkey");
        }

        private void PinToScreenHotkeyTextBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            HandleHotkeyPreviewKeyDown(sender, e, "PinToScreenHotkey");
        }

        private void HandleHotkeyPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e, string configPropertyName)
        {
            if (sender is System.Windows.Controls.TextBox textBox)
            {
                e.Handled = true;

                if (e.Key == Key.Delete || e.Key == Key.Back)
                {
                    textBox.Text = "";
                    UpdateConfigProperty(configPropertyName, "");
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
                UpdateConfigProperty(configPropertyName, hotkeyStr);
            }
        }

        private void UpdateConfigProperty(string propertyName, string value)
        {
            if (DataContext is Settings.SettingsViewModel settingsVM)
            {
                var config = settingsVM.Config;
                var prop = config.GetType().GetProperty(propertyName);
                prop?.SetValue(config, value);
            }
        }
    }
}

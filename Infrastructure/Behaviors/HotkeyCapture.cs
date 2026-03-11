using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace PromptMasterv6.Infrastructure.Behaviors
{
    public static class HotkeyCapture
    {
        public static readonly DependencyProperty HotkeyProperty =
            DependencyProperty.RegisterAttached(
                "Hotkey",
                typeof(string),
                typeof(HotkeyCapture),
                new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnHotkeyChanged));

        public static string GetHotkey(DependencyObject obj) => (string)obj.GetValue(HotkeyProperty);
        public static void SetHotkey(DependencyObject obj, string value) => obj.SetValue(HotkeyProperty, value);

        private static void OnHotkeyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is System.Windows.Controls.TextBox textBox)
            {
                if (e.NewValue is string hotkey)
                {
                    textBox.Text = hotkey;
                }
            }
        }

        public static readonly DependencyProperty EnabledProperty =
            DependencyProperty.RegisterAttached(
                "Enabled",
                typeof(bool),
                typeof(HotkeyCapture),
                new PropertyMetadata(false, OnEnabledChanged));

        public static bool GetEnabled(DependencyObject obj) => (bool)obj.GetValue(EnabledProperty);
        public static void SetEnabled(DependencyObject obj, bool value) => obj.SetValue(EnabledProperty, value);

        private static void OnEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is System.Windows.Controls.TextBox textBox)
            {
                if ((bool)e.NewValue)
                {
                    textBox.PreviewKeyDown += TextBox_PreviewKeyDown;
                    textBox.GotFocus += TextBox_GotFocus;
                }
                else
                {
                    textBox.PreviewKeyDown -= TextBox_PreviewKeyDown;
                    textBox.GotFocus -= TextBox_GotFocus;
                }
            }
        }

        private static void TextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.TextBox textBox)
            {
                textBox.Text = "按下快捷键...";
            }
        }

        private static void TextBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (sender is not System.Windows.Controls.TextBox textBox) return;

            e.Handled = true;

            var key = e.Key == Key.System ? e.SystemKey : e.Key;

            if (key == Key.Delete || key == Key.Back)
            {
                SetHotkey(textBox, string.Empty);
                textBox.Text = string.Empty;
                return;
            }

            if (key == Key.LeftCtrl || key == Key.RightCtrl ||
                key == Key.LeftAlt || key == Key.RightAlt ||
                key == Key.LeftShift || key == Key.RightShift ||
                key == Key.LWin || key == Key.RWin)
            {
                return;
            }

            var hotkeyStr = string.Empty;
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) hotkeyStr += "Ctrl+";
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt)) hotkeyStr += "Alt+";
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) hotkeyStr += "Shift+";
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Windows)) hotkeyStr += "Win+";

            hotkeyStr += key.ToString();

            SetHotkey(textBox, hotkeyStr);
            textBox.Text = hotkeyStr;
        }
    }
}

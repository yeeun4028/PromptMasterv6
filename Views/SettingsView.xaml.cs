using System;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using PromptMasterv5.Models;
using PromptMasterv5.Services;
using PromptMasterv5.ViewModels;
using WinFormsCursor = System.Windows.Forms.Cursor;

// 解决 WPF 和 WinForms 的命名冲突
using Button = System.Windows.Controls.Button;
using TextBox = System.Windows.Controls.TextBox;
using ListBox = System.Windows.Controls.ListBox;
using Color = System.Windows.Media.Color;

namespace PromptMasterv5.Views
{
    public partial class SettingsView : System.Windows.Controls.UserControl
    {
        private int _activeCoordinateRuleIndex = 0;

        public SettingsView()
        {
            InitializeComponent();
        }

        private MainViewModel? ViewModel => DataContext as MainViewModel;

        private void WebDavPasswordBox_Loaded(object sender, RoutedEventArgs e)
        {
            var pb = sender as PasswordBox;
            if (pb != null && ViewModel?.Config != null && pb.Password != ViewModel.Config.Password)
            {
                pb.Password = ViewModel.Config.Password;
            }
        }

        private void WebDavPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            var pb = sender as PasswordBox;
            if (pb != null && ViewModel?.Config != null)
            {
                ViewModel.Config.Password = pb.Password;
            }
        }

        private void FullWindowHotkeyTextBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (ViewModel == null) return;

            Key key = (e.Key == Key.System ? e.SystemKey : e.Key);

            // Delete to clear
            if (key == Key.Delete || key == Key.Back)
            {
                e.Handled = true;
                ViewModel.Config.FullWindowHotkey = "";
                (sender as TextBox)?.GetBindingExpression(TextBox.TextProperty)?.UpdateTarget();
                ViewModel.UpdateWindowHotkeys();
                return;
            }

            if (key == Key.LeftCtrl || key == Key.RightCtrl || key == Key.LeftAlt || key == Key.RightAlt || key == Key.LeftShift || key == Key.RightShift || key == Key.LWin || key == Key.RWin) return;
            e.Handled = true;
            var sb = new StringBuilder();
            if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control) sb.Append("Ctrl+");
            if ((Keyboard.Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt) sb.Append("Alt+");
            if ((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift) sb.Append("Shift+");
            if ((Keyboard.Modifiers & ModifierKeys.Windows) == ModifierKeys.Windows) sb.Append("Win+");
            sb.Append(key.ToString());
            
            if (sender is TextBox tb)
            {
                ViewModel.Config.FullWindowHotkey = sb.ToString();
                tb.GetBindingExpression(TextBox.TextProperty)?.UpdateTarget();
                ViewModel.UpdateWindowHotkeys();
            }
        }

        private void MiniWindowHotkeyTextBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (ViewModel == null) return;

            Key key = (e.Key == Key.System ? e.SystemKey : e.Key);

            // Delete to clear
            if (key == Key.Delete || key == Key.Back)
            {
                e.Handled = true;
                ViewModel.Config.MiniWindowHotkey = "";
                (sender as TextBox)?.GetBindingExpression(TextBox.TextProperty)?.UpdateTarget();
                ViewModel.UpdateWindowHotkeys();
                return;
            }

            if (key == Key.LeftCtrl || key == Key.RightCtrl || key == Key.LeftAlt || key == Key.RightAlt || key == Key.LeftShift || key == Key.RightShift || key == Key.LWin || key == Key.RWin) return;
            e.Handled = true;

            if (sender is TextBox tb)
            {
                var sb = new StringBuilder();
                if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control) sb.Append("Ctrl+");
                if ((Keyboard.Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt) sb.Append("Alt+");
                if ((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift) sb.Append("Shift+");
                if ((Keyboard.Modifiers & ModifierKeys.Windows) == ModifierKeys.Windows) sb.Append("Win+");
                sb.Append(key.ToString());

                ViewModel.Config.MiniWindowHotkey = sb.ToString();
                tb.GetBindingExpression(TextBox.TextProperty)?.UpdateTarget();
                ViewModel.UpdateWindowHotkeys();
            }
        }

        [System.Runtime.Versioning.SupportedOSPlatform("windows")]
        private async void PickCoordinate_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel == null) return;

            var btn = sender as Button; 
            if (btn == null) return; 
            string org = btn.Content.ToString() ?? "⏱️ 3秒后拾取";
            try
            {
                btn.IsEnabled = false;
                for (int i = 3; i > 0; i--) { btn.Content = $"{i}"; await Task.Delay(1000); }
                var pt = WinFormsCursor.Position;
                ViewModel.LocalConfig.ClickX = pt.X;
                ViewModel.LocalConfig.ClickY = pt.Y;

                var rules = ViewModel.LocalConfig.CoordinateRules;
                if (rules != null && rules.Count > 0)
                {
                    if (_activeCoordinateRuleIndex < 0) _activeCoordinateRuleIndex = 0;
                    if (_activeCoordinateRuleIndex >= rules.Count) _activeCoordinateRuleIndex = rules.Count - 1;

                    rules[_activeCoordinateRuleIndex].X = pt.X;
                    rules[_activeCoordinateRuleIndex].Y = pt.Y;
                }
                btn.Content = "已获取!";
                await Task.Delay(1000);
            }
            finally { btn.Content = org; btn.IsEnabled = true; }
        }

        [System.Runtime.Versioning.SupportedOSPlatform("windows")]
        private async void PickMiniWindowDefaultPosition_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel == null) return;

            var btn = sender as Button;
            if (btn == null) return;
            string org = btn.Content.ToString() ?? "⏱️3秒拾取";
            try
            {
                btn.IsEnabled = false;
                for (int i = 3; i > 0; i--) { btn.Content = $"{i}"; await Task.Delay(1000); }
                var pt = WinFormsCursor.Position;
                ViewModel.LocalConfig.MiniDefaultLeft = pt.X;
                ViewModel.LocalConfig.MiniDefaultBottom = pt.Y;
                btn.Content = "已获取!";
                await Task.Delay(1000);
            }
            finally { btn.Content = org; btn.IsEnabled = true; }
        }

        private void CoordinateRuleField_GotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe)
            {
                if (fe.Tag is int idx)
                {
                    _activeCoordinateRuleIndex = idx;
                }
                else if (fe.Tag is string s && int.TryParse(s, out int parsed))
                {
                    _activeCoordinateRuleIndex = parsed;
                }
            }
        }

        private void AddCoordinateRule_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel == null) return;

            if (ViewModel.LocalConfig.CoordinateRules == null)
            {
                ViewModel.LocalConfig.CoordinateRules = new();
            }

            ViewModel.LocalConfig.CoordinateRules.Add(new CoordinateRule());
            _activeCoordinateRuleIndex = ViewModel.LocalConfig.CoordinateRules.Count - 1;
        }

        private async void TestAiConnection_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel == null) return;

            var btn = sender as Button;
            if (btn == null) return;

            var statusText = this.FindName("TestStatusText") as TextBlock;
            if (statusText == null) return;

            try
            {
                statusText.Text = "🔄 测试中...";
                statusText.Foreground = new SolidColorBrush(Color.FromRgb(102, 102, 102));
                btn.IsEnabled = false;

                var aiService = new AiService();
                (bool success, string message) = await aiService.TestConnectionAsync(ViewModel.Config);

                if (success)
                {
                    statusText.Text = "✅ 成功连通";
                    statusText.Foreground = new SolidColorBrush(Color.FromRgb(67, 160, 71));

                    await Task.Delay(3000);
                    statusText.Text = "";
                }
                else
                {
                    statusText.Text = "❌ 连通失败";
                    statusText.Foreground = new SolidColorBrush(Color.FromRgb(229, 57, 53));
                }
            }
            catch (Exception)
            {
                statusText.Text = "❌ 连接异常";
                statusText.Foreground = new SolidColorBrush(Color.FromRgb(229, 57, 53));
            }
            finally
            {
                btn.IsEnabled = true;
            }
        }

        private void AiModelList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ViewModel == null) return;

            var listBox = sender as ListBox;
            if (listBox == null) return;

            if (listBox.SelectedItem is AiModelConfig selectedModel)
            {
                ViewModel.ActivateAiModelCommand.Execute(selectedModel);
            }
        }
    }
}

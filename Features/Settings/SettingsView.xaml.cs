using System;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.IO;
using System.Linq;
using PromptMasterv6.Core.Models;
using PromptMasterv6.Infrastructure.Services;

using Button = System.Windows.Controls.Button;
using TextBox = System.Windows.Controls.TextBox;
using ListBox = System.Windows.Controls.ListBox;
using Color = System.Windows.Media.Color;

namespace PromptMasterv6.Features.Settings
{
    public partial class SettingsView : System.Windows.Controls.UserControl
    {
        private int _selectedExternalToolsSubTab = 0;
        private int _selectedAiSubTab = 0;

        public SettingsView()
        {
            InitializeComponent();
            Loaded += SettingsView_Loaded;
        }

        private void SettingsView_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateExternalToolsSubTab(0);
            UpdateAiSubTab(0);
            UpdateSyncSubTab(0);
        }

        private SettingsViewModel? ViewModel => DataContext as SettingsViewModel;

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

        private void LauncherHotkeyTextBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (ViewModel == null) return;

            Key key = (e.Key == Key.System ? e.SystemKey : e.Key);

            if (key == Key.Delete || key == Key.Back)
            {
                e.Handled = true;
                ViewModel.Config.LauncherHotkey = "";
                (sender as TextBox)?.GetBindingExpression(TextBox.TextProperty)?.UpdateTarget();
                ViewModel.UpdateLauncherHotkey();
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

                ViewModel.Config.LauncherHotkey = sb.ToString();
                tb.GetBindingExpression(TextBox.TextProperty)?.UpdateTarget();
                ViewModel.UpdateLauncherHotkey();
            }
        }

        private void TranslateHotkeyTextBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (ViewModel == null) return;
            CaptureHotkeyToString(e, value =>
            {
                ViewModel.LocalConfig.TranslateHotkey = value;
                LocalConfigService.Save(ViewModel.LocalConfig);
                ViewModel.UpdateWindowHotkeys();
            });
        }

        private static void CaptureHotkeyToString(System.Windows.Input.KeyEventArgs e, Action<string> setValue)
        {
            Key key = (e.Key == Key.System ? e.SystemKey : e.Key);

            if (key == Key.Delete || key == Key.Back)
            {
                e.Handled = true;
                setValue("");
                return;
            }

            if (key == Key.LeftCtrl || key == Key.RightCtrl ||
                key == Key.LeftAlt || key == Key.RightAlt ||
                key == Key.LeftShift || key == Key.RightShift ||
                key == Key.LWin || key == Key.RWin)
            {
                return;
            }

            e.Handled = true;

            var sb = new StringBuilder();
            if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control) sb.Append("Ctrl+");
            if ((Keyboard.Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt) sb.Append("Alt+");
            if ((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift) sb.Append("Shift+");
            if ((Keyboard.Modifiers & ModifierKeys.Windows) == ModifierKeys.Windows) sb.Append("Win+");
            sb.Append(key.ToString());

            setValue(sb.ToString());
        }

        private void ScreenshotTranslateHotkeyTextBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (ViewModel == null) return;

            Key key = (e.Key == Key.System ? e.SystemKey : e.Key);

            if (key == Key.Delete || key == Key.Back)
            {
                e.Handled = true;
                ViewModel.Config.ScreenshotTranslateHotkey = "";
                (sender as TextBox)?.GetBindingExpression(TextBox.TextProperty)?.UpdateTarget();
                ViewModel.UpdateExternalToolsHotkeys();
                return;
            }

            if (key == Key.LeftCtrl || key == Key.RightCtrl || key == Key.LeftAlt || key == Key.RightAlt || 
                key == Key.LeftShift || key == Key.RightShift || key == Key.LWin || key == Key.RWin) return;
            
            e.Handled = true;
            var sb = new StringBuilder();
            if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control) sb.Append("Ctrl+");
            if ((Keyboard.Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt) sb.Append("Alt+");
            if ((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift) sb.Append("Shift+");
            if ((Keyboard.Modifiers & ModifierKeys.Windows) == ModifierKeys.Windows) sb.Append("Win+");
            sb.Append(key.ToString());
            
            if (sender is TextBox tb)
            {
                ViewModel.Config.ScreenshotTranslateHotkey = sb.ToString();
                tb.GetBindingExpression(TextBox.TextProperty)?.UpdateTarget();
                ViewModel.UpdateExternalToolsHotkeys();
            }
        }

        private void OcrHotkeyTextBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (ViewModel == null) return;

            Key key = (e.Key == Key.System ? e.SystemKey : e.Key);

            if (key == Key.Delete || key == Key.Back)
            {
                e.Handled = true;
                ViewModel.Config.OcrHotkey = "";
                (sender as TextBox)?.GetBindingExpression(TextBox.TextProperty)?.UpdateTarget();
                ViewModel.UpdateExternalToolsHotkeys();
                return;
            }

            if (key == Key.LeftCtrl || key == Key.RightCtrl || key == Key.LeftAlt || key == Key.RightAlt || 
                key == Key.LeftShift || key == Key.RightShift || key == Key.LWin || key == Key.RWin) return;
            
            e.Handled = true;
            var sb = new StringBuilder();
            if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control) sb.Append("Ctrl+");
            if ((Keyboard.Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt) sb.Append("Alt+");
            if ((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift) sb.Append("Shift+");
            if ((Keyboard.Modifiers & ModifierKeys.Windows) == ModifierKeys.Windows) sb.Append("Win+");
            sb.Append(key.ToString());
            
            if (sender is TextBox tb)
            {
                ViewModel.Config.OcrHotkey = sb.ToString();
                tb.GetBindingExpression(TextBox.TextProperty)?.UpdateTarget();
                ViewModel.UpdateExternalToolsHotkeys();
            }
        }

        private void PinToScreenHotkeyTextBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (ViewModel == null) return;

            Key key = (e.Key == Key.System ? e.SystemKey : e.Key);

            if (key == Key.Delete || key == Key.Back)
            {
                e.Handled = true;
                ViewModel.Config.PinToScreenHotkey = "";
                (sender as TextBox)?.GetBindingExpression(TextBox.TextProperty)?.UpdateTarget();
                ViewModel.UpdateExternalToolsHotkeys();
                return;
            }

            if (key == Key.LeftCtrl || key == Key.RightCtrl || key == Key.LeftAlt || key == Key.RightAlt || 
                key == Key.LeftShift || key == Key.RightShift || key == Key.LWin || key == Key.RWin) return;
            
            e.Handled = true;
            var sb = new StringBuilder();
            if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control) sb.Append("Ctrl+");
            if ((Keyboard.Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt) sb.Append("Alt+");
            if ((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift) sb.Append("Shift+");
            if ((Keyboard.Modifiers & ModifierKeys.Windows) == ModifierKeys.Windows) sb.Append("Win+");
            sb.Append(key.ToString());
            
            if (sender is TextBox tb2)
            {
                ViewModel.Config.PinToScreenHotkey = sb.ToString();
                tb2.GetBindingExpression(TextBox.TextProperty)?.UpdateTarget();
                ViewModel.UpdateExternalToolsHotkeys();
            }
        }

        private void LaunchBarHotkeyTextBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (ViewModel == null) return;

            Key key = (e.Key == Key.System ? e.SystemKey : e.Key);

            if (key == Key.Delete || key == Key.Back)
            {
                e.Handled = true;
                ViewModel.Config.LaunchBarHotkey = "";
                (sender as TextBox)?.GetBindingExpression(TextBox.TextProperty)?.UpdateTarget();
                ViewModel.UpdateWindowHotkeys();
                return;
            }

            if (key == Key.LeftCtrl || key == Key.RightCtrl || key == Key.LeftAlt || key == Key.RightAlt || 
                key == Key.LeftShift || key == Key.RightShift || key == Key.LWin || key == Key.RWin) return;
            
            e.Handled = true;
            var sb = new StringBuilder();
            if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control) sb.Append("Ctrl+");
            if ((Keyboard.Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt) sb.Append("Alt+");
            if ((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift) sb.Append("Shift+");
            if ((Keyboard.Modifiers & ModifierKeys.Windows) == ModifierKeys.Windows) sb.Append("Win+");
            sb.Append(key.ToString());
            
            if (sender is TextBox tb)
            {
                ViewModel.Config.LaunchBarHotkey = sb.ToString();
                tb.GetBindingExpression(TextBox.TextProperty)?.UpdateTarget();
                ViewModel.UpdateWindowHotkeys();
            }
        }

        private void ExternalToolsSubTab_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string tagStr && int.TryParse(tagStr, out int tabIndex))
            {
                UpdateExternalToolsSubTab(tabIndex);
            }
        }

        private void UpdateExternalToolsSubTab(int tabIndex)
        {
            _selectedExternalToolsSubTab = tabIndex;

            if (BtnMainTab != null) BtnMainTab.Tag = tabIndex == 0 ? "Selected" : "0";
            if (BtnBaiduTab != null) BtnBaiduTab.Tag = tabIndex == 1 ? "Selected" : "1";
            if (BtnTencentTab != null) BtnTencentTab.Tag = tabIndex == 2 ? "Selected" : "2";
            if (BtnYoudaoTab != null) BtnYoudaoTab.Tag = tabIndex == 3 ? "Selected" : "3";
            if (BtnGoogleTab != null) BtnGoogleTab.Tag = tabIndex == 4 ? "Selected" : "4";
            if (BtnExternalAiTranslateTab != null) BtnExternalAiTranslateTab.Tag = tabIndex == 5 ? "Selected" : "5";

            if (ExternalToolsMainTab != null) ExternalToolsMainTab.Visibility = tabIndex == 0 ? Visibility.Visible : Visibility.Collapsed;
            if (ExternalToolsBaiduTab != null) ExternalToolsBaiduTab.Visibility = tabIndex == 1 ? Visibility.Visible : Visibility.Collapsed;
            if (ExternalToolsTencentTab != null) ExternalToolsTencentTab.Visibility = tabIndex == 2 ? Visibility.Visible : Visibility.Collapsed;
            if (ExternalToolsYoudaoTab != null) ExternalToolsYoudaoTab.Visibility = tabIndex == 3 ? Visibility.Visible : Visibility.Collapsed;
            if (ExternalToolsGoogleTab != null) ExternalToolsGoogleTab.Visibility = tabIndex == 4 ? Visibility.Visible : Visibility.Collapsed;
            if (ExternalToolsAiTranslateTab != null) ExternalToolsAiTranslateTab.Visibility = tabIndex == 5 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void AiSubTab_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string tagStr)
            {
                int tabIndex = tagStr == "Selected" ? _selectedAiSubTab : (int.TryParse(tagStr, out int idx) ? idx : 0);
                UpdateAiSubTab(tabIndex);
            }
        }

        private void UpdateAiSubTab(int tabIndex)
        {
            _selectedAiSubTab = tabIndex;

            if (AiMainTab != null) AiMainTab.Visibility = tabIndex == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private int _selectedSyncSubTab = 0;

        private void SyncSubTab_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string tagStr)
            {
                int tabIndex = tagStr == "Selected" ? _selectedSyncSubTab : (int.TryParse(tagStr, out int idx) ? idx : 0);
                UpdateSyncSubTab(tabIndex);
            }
        }

        private void UpdateSyncSubTab(int tabIndex)
        {
            _selectedSyncSubTab = tabIndex;

            if (BtnSyncWebDavTab != null) BtnSyncWebDavTab.Tag = tabIndex == 0 ? "Selected" : "0";
            if (BtnSyncDataTab != null) BtnSyncDataTab.Tag = tabIndex == 1 ? "Selected" : "1";
            if (BtnSyncLogTab != null) BtnSyncLogTab.Tag = tabIndex == 2 ? "Selected" : "2";

            if (SyncWebDavTab != null) SyncWebDavTab.Visibility = tabIndex == 0 ? Visibility.Visible : Visibility.Collapsed;
            if (SyncDataTab != null) SyncDataTab.Visibility = tabIndex == 1 ? Visibility.Visible : Visibility.Collapsed;
            if (SyncLogTab != null) SyncLogTab.Visibility = tabIndex == 2 ? Visibility.Visible : Visibility.Collapsed;
        }
    }
}

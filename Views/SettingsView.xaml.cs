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
using PromptMasterv6.ViewModels;
using WinFormsCursor = System.Windows.Forms.Cursor;

// 解决 WPF 和 WinForms 的命名冲突
using Button = System.Windows.Controls.Button;
using TextBox = System.Windows.Controls.TextBox;
using ListBox = System.Windows.Controls.ListBox;
using Color = System.Windows.Media.Color;

namespace PromptMasterv6.Views
{
    public partial class SettingsView : System.Windows.Controls.UserControl
    {
        private int _selectedExternalToolsSubTab = 0;

        private int _selectedAiSubTab = 0;
        private int _selectedVoiceControlSubTab = 0;

        public SettingsView()
        {
            InitializeComponent();
            Loaded += SettingsView_Loaded;
        }

        private void SettingsView_Loaded(object sender, RoutedEventArgs e)
        {
            // Initialize external tools sub-tab to Main tab
            UpdateExternalToolsSubTab(0);
            

            // Initialize AI sub-tab to Main tab
            UpdateAiSubTab(0);

            // Initialize Sync sub-tab to WebDAV tab
            UpdateSyncSubTab(0);

            // Initialize Voice Control sub-tab to Engine tab
            UpdateVoiceControlSubTab(0);
            
            // Subscribe to VoiceProvider changes
            if (ViewModel?.Config != null)
            {
                ViewModel.Config.PropertyChanged += (s, ev) =>
                {
                    if (ev.PropertyName == nameof(AppConfig.VoiceProvider))
                    {
                        UpdateVoiceProviderUI();
                    }
                };
            }
        }

        // Baidu and Tencent credentials methods moved to SettingsViewModel

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


        private void LauncherHotkeyTextBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (ViewModel == null) return;

            Key key = (e.Key == Key.System ? e.SystemKey : e.Key);

            // Delete to clear
            if (key == Key.Delete || key == Key.Back)
            {
                e.Handled = true;
                ViewModel.Config.LauncherHotkey = "";
                (sender as TextBox)?.GetBindingExpression(TextBox.TextProperty)?.UpdateTarget();
                ViewModel.SettingsVM.UpdateLauncherHotkey();
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
                ViewModel.SettingsVM.UpdateLauncherHotkey();
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

            // Delete to clear
            if (key == Key.Delete || key == Key.Back)
            {
                e.Handled = true;
                ViewModel.Config.ScreenshotTranslateHotkey = "";
                (sender as TextBox)?.GetBindingExpression(TextBox.TextProperty)?.UpdateTarget();
                ViewModel.SettingsVM.UpdateExternalToolsHotkeys();
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
                ViewModel.SettingsVM.UpdateExternalToolsHotkeys();
            }
        }


        private void OcrHotkeyTextBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (ViewModel == null) return;

            Key key = (e.Key == Key.System ? e.SystemKey : e.Key);

            // Delete to clear
            if (key == Key.Delete || key == Key.Back)
            {
                e.Handled = true;
                ViewModel.Config.OcrHotkey = "";
                (sender as TextBox)?.GetBindingExpression(TextBox.TextProperty)?.UpdateTarget();
                ViewModel.SettingsVM.UpdateExternalToolsHotkeys();
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
                ViewModel.SettingsVM.UpdateExternalToolsHotkeys();
            }
        }

        private void PinToScreenHotkeyTextBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (ViewModel == null) return;

            Key key = (e.Key == Key.System ? e.SystemKey : e.Key);

            // Delete to clear
            if (key == Key.Delete || key == Key.Back)
            {
                e.Handled = true;
                ViewModel.Config.PinToScreenHotkey = "";
                (sender as TextBox)?.GetBindingExpression(TextBox.TextProperty)?.UpdateTarget();
                ViewModel.SettingsVM.UpdateExternalToolsHotkeys();
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
                ViewModel.SettingsVM.UpdateExternalToolsHotkeys();
            }
        }

        private void LaunchBarHotkeyTextBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (ViewModel == null) return;

            Key key = (e.Key == Key.System ? e.SystemKey : e.Key);

            // Delete to clear
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


        private void VoiceTriggerHotkeyTextBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (ViewModel == null) return;

            Key key = (e.Key == Key.System ? e.SystemKey : e.Key);

            // Delete to clear
            if (key == Key.Delete || key == Key.Back)
            {
                e.Handled = true;
                ViewModel.Config.VoiceTriggerHotkey = "";
                (sender as TextBox)?.GetBindingExpression(TextBox.TextProperty)?.UpdateTarget();
                ViewModel.SettingsVM.UpdateVoiceTriggerHotkey();
                return;
            }

            e.Handled = true;

            // Voice control hotkey uses explicit Key names, including for modifiers.
            // Let's gather currently pressed modifiers exactly (Left/Right)
            var sb = new StringBuilder();

            bool lCtrl = Keyboard.IsKeyDown(Key.LeftCtrl);
            bool rCtrl = Keyboard.IsKeyDown(Key.RightCtrl);
            bool lAlt = Keyboard.IsKeyDown(Key.LeftAlt);
            bool rAlt = Keyboard.IsKeyDown(Key.RightAlt);
            bool lShift = Keyboard.IsKeyDown(Key.LeftShift);
            bool rShift = Keyboard.IsKeyDown(Key.RightShift);
            bool lWin = Keyboard.IsKeyDown(Key.LWin);
            bool rWin = Keyboard.IsKeyDown(Key.RWin);

            // If the key strictly IS a modifier, we don't treat it as the "main" key if other modifiers are pressed, 
            // actually we just collect what's pressed. But a hotkey is usually modifiers + 1 main key.
            // If the user presses LCtrl, it fires KeyDown(LCtrl). 
            // If they hold LCtrl and press T, it fires KeyDown(T).
            // So we add all pressed modifiers first.
            
            bool isModifierKey = key == Key.LeftCtrl || key == Key.RightCtrl || key == Key.LeftAlt || key == Key.RightAlt || key == Key.LeftShift || key == Key.RightShift || key == Key.LWin || key == Key.RWin;

            // We want to avoid adding the key twice if it's both in IsKeyDown and e.Key
            if (lCtrl && key != Key.LeftCtrl) sb.Append("LeftCtrl+");
            if (rCtrl && key != Key.RightCtrl) sb.Append("RightCtrl+");
            if (lAlt && key != Key.LeftAlt) sb.Append("LeftAlt+");
            if (rAlt && key != Key.RightAlt) sb.Append("RightAlt+");
            if (lShift && key != Key.LeftShift) sb.Append("LeftShift+");
            if (rShift && key != Key.RightShift) sb.Append("RightShift+");
            if (lWin && key != Key.LWin) sb.Append("LWin+");
            if (rWin && key != Key.RWin) sb.Append("RWin+");

            sb.Append(key.ToString());

            if (sender is TextBox tb)
            {
                ViewModel.Config.VoiceTriggerHotkey = sb.ToString();
                tb.GetBindingExpression(TextBox.TextProperty)?.UpdateTarget();
                ViewModel.SettingsVM.UpdateVoiceTriggerHotkey();
            }
        }


        // External Tools Sub-Tab Navigation
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

            // Update button states directly - much simpler!
            if (BtnMainTab != null) BtnMainTab.Tag = tabIndex == 0 ? "Selected" : "0";
            if (BtnBaiduTab != null) BtnBaiduTab.Tag = tabIndex == 1 ? "Selected" : "1";
            if (BtnTencentTab != null) BtnTencentTab.Tag = tabIndex == 2 ? "Selected" : "2";
            if (BtnYoudaoTab != null) BtnYoudaoTab.Tag = tabIndex == 3 ? "Selected" : "3";
            if (BtnGoogleTab != null) BtnGoogleTab.Tag = tabIndex == 4 ? "Selected" : "4";
            if (BtnExternalAiTranslateTab != null) BtnExternalAiTranslateTab.Tag = tabIndex == 5 ? "Selected" : "5";


            // Show/hide tab content
            if (ExternalToolsMainTab != null) ExternalToolsMainTab.Visibility = tabIndex == 0 ? Visibility.Visible : Visibility.Collapsed;
            if (ExternalToolsBaiduTab != null) ExternalToolsBaiduTab.Visibility = tabIndex == 1 ? Visibility.Visible : Visibility.Collapsed;
            if (ExternalToolsTencentTab != null) ExternalToolsTencentTab.Visibility = tabIndex == 2 ? Visibility.Visible : Visibility.Collapsed;
            if (ExternalToolsYoudaoTab != null) ExternalToolsYoudaoTab.Visibility = tabIndex == 3 ? Visibility.Visible : Visibility.Collapsed;
            if (ExternalToolsGoogleTab != null) ExternalToolsGoogleTab.Visibility = tabIndex == 4 ? Visibility.Visible : Visibility.Collapsed;
            if (ExternalToolsAiTranslateTab != null) ExternalToolsAiTranslateTab.Visibility = tabIndex == 5 ? Visibility.Visible : Visibility.Collapsed;
        }

        // AI Sub-Tab Navigation
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

            // Tab 2 (Translations) and Tab 3 (Selection Assistant) removed

            // Show/hide tab content
            if (AiMainTab != null) AiMainTab.Visibility = tabIndex == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        // Sync Sub-Tab Handlers
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

            // Update button states
            if (BtnSyncWebDavTab != null) BtnSyncWebDavTab.Tag = tabIndex == 0 ? "Selected" : "0";
            if (BtnSyncDataTab != null) BtnSyncDataTab.Tag = tabIndex == 1 ? "Selected" : "1";
            if (BtnSyncLogTab != null) BtnSyncLogTab.Tag = tabIndex == 2 ? "Selected" : "2";

            // Show/hide tab content
            if (SyncWebDavTab != null) SyncWebDavTab.Visibility = tabIndex == 0 ? Visibility.Visible : Visibility.Collapsed;
            if (SyncDataTab != null) SyncDataTab.Visibility = tabIndex == 1 ? Visibility.Visible : Visibility.Collapsed;
            if (SyncLogTab != null) SyncLogTab.Visibility = tabIndex == 2 ? Visibility.Visible : Visibility.Collapsed;
        }

        // Google credentials methods moved to SettingsViewModel

        // AI Translation Config methods moved to SettingsViewModel

        // Connection Test Methods
        // Baidu test methods moved to SettingsViewModel
        // Tencent, Youdao, Google, Xunfei test methods moved to SettingsViewModel

        #region Voice Control Sub-Tab Navigation

        private void VoiceControlSubTab_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string tagStr && int.TryParse(tagStr, out int tabIndex))
            {
                UpdateVoiceControlSubTab(tabIndex);
            }
        }

        private void UpdateVoiceControlSubTab(int tabIndex)
        {
            _selectedVoiceControlSubTab = tabIndex;

            // Update button states
            if (BtnVoiceEngineTab != null) BtnVoiceEngineTab.Tag = tabIndex == 0 ? "Selected" : "0";
            if (BtnXunfeiConfigTab != null) BtnXunfeiConfigTab.Tag = tabIndex == 1 ? "Selected" : "1";
            if (BtnVoiceCommandsTab != null) BtnVoiceCommandsTab.Tag = tabIndex == 2 ? "Selected" : "2";

            // Show/hide tab content
            if (VoiceEngineTab != null) VoiceEngineTab.Visibility = tabIndex == 0 ? Visibility.Visible : Visibility.Collapsed;
            if (XunfeiConfigTab != null) XunfeiConfigTab.Visibility = tabIndex == 1 ? Visibility.Visible : Visibility.Collapsed;
            if (VoiceCommandsTab != null) VoiceCommandsTab.Visibility = tabIndex == 2 ? Visibility.Visible : Visibility.Collapsed;

            // Update voice provider UI
            UpdateVoiceProviderUI();
        }

        private void UpdateVoiceProviderUI()
        {
            if (ViewModel?.Config == null) return;

            var isXunfei = ViewModel.Config.VoiceProvider == VoiceProvider.Xunfei;

            // Show/hide OpenAI model selection
            if (OpenAIModelSelection != null)
                OpenAIModelSelection.Visibility = isXunfei ? Visibility.Collapsed : Visibility.Visible;
        }

        #endregion

        #region Xunfei Configuration

        private void XunfeiApiSecretBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (ViewModel == null) return;
            if (sender is PasswordBox pb)
            {
                ViewModel.Config.XunfeiApiSecret = pb.Password;
            }
        }

        #endregion
    }
}

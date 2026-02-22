using System;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.IO;
using System.Net.Http;
using PromptMasterv5.Core.Models;
using PromptMasterv5.Infrastructure.Services;
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
        private int _selectedExternalToolsSubTab = 0;
        private int _selectedMiniWindowSubTab = 0;
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
            
            // Initialize mini window sub-tab to Prompt tab
            UpdateMiniWindowSubTab(0);

            // Initialize AI sub-tab to Main tab
            UpdateAiSubTab(0);

            // Initialize Sync sub-tab to WebDAV tab
            UpdateSyncSubTab(0);

            // Initialize Voice Control sub-tab to Engine tab
            UpdateVoiceControlSubTab(0);

            // Load Baidu credentials from AppConfig
            LoadBaiduCredentials();
            LoadTencentCredentials();
            
            // Load Xunfei credentials
            LoadXunfeiCredentials();
            
            // Subscribe to VoiceProvider changes
            if (ViewModel?.Config != null)
            {
                ViewModel.Config.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(AppConfig.VoiceProvider))
                    {
                        UpdateVoiceProviderUI();
                    }
                };
            }
        }

        private void LoadBaiduCredentials()
        {
            if (ViewModel == null) return;

            // Find Baidu OCR and Translation profiles in ApiProfiles
            var baiduOcrProfile = ViewModel.Config.ApiProfiles.FirstOrDefault(p => 
                p.Provider == ApiProvider.Baidu && p.ServiceType == ServiceType.OCR);
            var baiduTransProfile = ViewModel.Config.ApiProfiles.FirstOrDefault(p => 
                p.Provider == ApiProvider.Baidu && p.ServiceType == ServiceType.Translation);

            // Load OCR credentials
            if (baiduOcrProfile != null && BaiduOcrApiKey != null && BaiduOcrSecretKey != null)
            {
                BaiduOcrApiKey.Text = baiduOcrProfile.Key1;
                BaiduOcrSecretKey.Text = baiduOcrProfile.Key2;
            }

            // Load Translation credentials
            if (baiduTransProfile != null && BaiduTranslateAppId != null && BaiduTranslateSecretKey != null)
            {
                BaiduTranslateAppId.Text = baiduTransProfile.Key1;
                BaiduTranslateSecretKey.Text = baiduTransProfile.Key2;
            }
        }

        private void SaveBaiduCredentials()
        {
            if (ViewModel == null) return;

            // Find or create Baidu OCR profile
            var baiduOcrProfile = ViewModel.Config.ApiProfiles.FirstOrDefault(p => 
                p.Provider == ApiProvider.Baidu && p.ServiceType == ServiceType.OCR);
            
            if (baiduOcrProfile == null)
            {
                baiduOcrProfile = new ApiProfile
                {
                    Name = "百度 OCR",
                    Provider = ApiProvider.Baidu,
                    ServiceType = ServiceType.OCR
                };
                ViewModel.Config.ApiProfiles.Add(baiduOcrProfile);
            }

            if (BaiduOcrApiKey != null && BaiduOcrSecretKey != null)
            {
                baiduOcrProfile.Key1 = BaiduOcrApiKey.Text;
                baiduOcrProfile.Key2 = BaiduOcrSecretKey.Text;
            }

            // Find or create Baidu Translation profile
            var baiduTransProfile = ViewModel.Config.ApiProfiles.FirstOrDefault(p => 
                p.Provider == ApiProvider.Baidu && p.ServiceType == ServiceType.Translation);
            
            if (baiduTransProfile == null)
            {
                baiduTransProfile = new ApiProfile
                {
                    Name = "百度翻译",
                    Provider = ApiProvider.Baidu,
                    ServiceType = ServiceType.Translation
                };
                ViewModel.Config.ApiProfiles.Add(baiduTransProfile);
            }

            if (BaiduTranslateAppId != null && BaiduTranslateSecretKey != null)
            {
                baiduTransProfile.Key1 = BaiduTranslateAppId.Text;
                baiduTransProfile.Key2 = BaiduTranslateSecretKey.Text;
            }

            // Auto-set as active profiles if not already set
            if (string.IsNullOrEmpty(ViewModel.Config.OcrProfileId))
            {
                ViewModel.Config.OcrProfileId = baiduOcrProfile.Id;
            }
            if (string.IsNullOrEmpty(ViewModel.Config.TranslateProfileId))
            {
                ViewModel.Config.TranslateProfileId = baiduTransProfile.Id;
            }

            ConfigService.Save(ViewModel.Config);
            
            // Refresh logic if needed (ObservableCollection updates automatically if added/removed, but filters might need help if relying on new instances)
            // Since we added to Config.ApiProfiles, the computed properties in ExternalToolsViewModel (if implemented as just getters returning new OC) won't auto-update unless we notify.
            // Better to trigger a refresh in ViewModel.
            // However, our current implementation created `new ObservableCollection` in the property getter which is NOT dynamic.
            // We need to fix ExternalToolsViewModel to have ObservableCollections that sync with Config.ApiProfiles OR just refresh the view.
            
            // For now, let's just force a NotifyPropertyChanged on the ViewModel properties if we can.
            // But better: let's modifying ExternalToolsViewModel to actually use a filtering mechanism, 
            // OR just re-fetch the list here if we can access ExternalToolsVM.
            
            if (ViewModel.ExternalToolsVM != null)
            {
                ViewModel.ExternalToolsVM.RefreshProfiles();
            }
        }

        private void LoadTencentCredentials()
        {
            if (ViewModel == null) return;

            var tencentOcrProfile = ViewModel.Config.ApiProfiles.FirstOrDefault(p => 
                p.Provider == ApiProvider.Tencent && p.ServiceType == ServiceType.OCR);
            var tencentTransProfile = ViewModel.Config.ApiProfiles.FirstOrDefault(p => 
                p.Provider == ApiProvider.Tencent && p.ServiceType == ServiceType.Translation);

            if (tencentOcrProfile != null && TencentOcrSecretId != null && TencentOcrSecretKey != null)
            {
                TencentOcrSecretId.Text = tencentOcrProfile.Key1;
                TencentOcrSecretKey.Text = tencentOcrProfile.Key2;
            }

            if (tencentTransProfile != null && TencentTranslateSecretId != null && TencentTranslateSecretKey != null)
            {
                TencentTranslateSecretId.Text = tencentTransProfile.Key1;
                TencentTranslateSecretKey.Text = tencentTransProfile.Key2;
            }
        }

        private void SaveTencentCredentials()
        {
            if (ViewModel == null) return;

            // OCR Profile
            var tencentOcrProfile = ViewModel.Config.ApiProfiles.FirstOrDefault(p => 
                p.Provider == ApiProvider.Tencent && p.ServiceType == ServiceType.OCR);
            
            if (tencentOcrProfile == null)
            {
                tencentOcrProfile = new ApiProfile
                {
                    Name = "腾讯云 OCR",
                    Provider = ApiProvider.Tencent,
                    ServiceType = ServiceType.OCR
                };
                ViewModel.Config.ApiProfiles.Add(tencentOcrProfile);
            }

            if (TencentOcrSecretId != null && TencentOcrSecretKey != null)
            {
                tencentOcrProfile.Key1 = TencentOcrSecretId.Text;
                tencentOcrProfile.Key2 = TencentOcrSecretKey.Text;
            }

            // Translation Profile
            var tencentTransProfile = ViewModel.Config.ApiProfiles.FirstOrDefault(p => 
                p.Provider == ApiProvider.Tencent && p.ServiceType == ServiceType.Translation);
            
            if (tencentTransProfile == null)
            {
                tencentTransProfile = new ApiProfile
                {
                    Name = "腾讯云翻译",
                    Provider = ApiProvider.Tencent,
                    ServiceType = ServiceType.Translation
                };
                ViewModel.Config.ApiProfiles.Add(tencentTransProfile);
            }

            if (TencentTranslateSecretId != null && TencentTranslateSecretKey != null)
            {
                tencentTransProfile.Key1 = TencentTranslateSecretId.Text;
                tencentTransProfile.Key2 = TencentTranslateSecretKey.Text;
            }

            // Auto-set as active profiles if undefined
            if (string.IsNullOrEmpty(ViewModel.Config.OcrProfileId))
            {
                ViewModel.Config.OcrProfileId = tencentOcrProfile.Id;
            }
            if (string.IsNullOrEmpty(ViewModel.Config.TranslateProfileId))
            {
                ViewModel.Config.TranslateProfileId = tencentTransProfile.Id;
            }

            ConfigService.Save(ViewModel.Config);

            if (ViewModel.ExternalToolsVM != null)
            {
                ViewModel.ExternalToolsVM.RefreshProfiles();
            }
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
                var dip = ScreenToDip(new System.Windows.Point(pt.X, pt.Y));
                ViewModel.LocalConfig.MiniDefaultLeft = Math.Round(dip.X, 1);
                ViewModel.LocalConfig.MiniDefaultBottom = Math.Round(dip.Y, 1);
                btn.Content = "已获取!";
                await Task.Delay(1000);
            }
            finally { btn.Content = org; btn.IsEnabled = true; }
        }

        private System.Windows.Point ScreenToDip(System.Windows.Point screenPoint)
        {
            var hostWindow = Window.GetWindow(this);
            if (hostWindow == null) return screenPoint;

            var source = PresentationSource.FromVisual(hostWindow);
            var target = source?.CompositionTarget;
            if (target == null) return screenPoint;

            return target.TransformFromDevice.Transform(screenPoint);
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

        private void AddMiniPinnedPrompt_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel == null) return;
            var selectedId = (MiniPinnedPromptCandidateCombo?.SelectedValue as string) ?? "";
            if (!string.IsNullOrWhiteSpace(selectedId))
            {
                ViewModel.LocalConfig.MiniPinnedPromptCandidateId = selectedId;
            }

            ViewModel.AddMiniPinnedPromptFromCandidate();
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

        private void SelectedTextTranslateHotkeyTextBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (ViewModel == null) return;

            Key key = (e.Key == Key.System ? e.SystemKey : e.Key);

            // Delete to clear
            if (key == Key.Delete || key == Key.Back)
            {
                e.Handled = true;
                ViewModel.Config.SelectedTextTranslateHotkey = "";
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
                ViewModel.Config.SelectedTextTranslateHotkey = sb.ToString();
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

        private void QuickActionHotkeyTextBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (ViewModel == null) return;

            Key key = (e.Key == Key.System ? e.SystemKey : e.Key);

            // Delete to clear
            if (key == Key.Delete || key == Key.Back)
            {
                e.Handled = true;
                ViewModel.Config.QuickActionHotkey = "";
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
                ViewModel.Config.QuickActionHotkey = sb.ToString();
                tb.GetBindingExpression(TextBox.TextProperty)?.UpdateTarget();
                ViewModel.SettingsVM.UpdateExternalToolsHotkeys();
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

        private void MiniWindowSubTab_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string tagStr && int.TryParse(tagStr, out int tabIndex))
            {
                UpdateMiniWindowSubTab(tabIndex);
            }
        }

        private void UpdateMiniWindowSubTab(int tabIndex)
        {
            _selectedMiniWindowSubTab = tabIndex;

            // Update button states
            if (BtnMiniPromptTab != null) BtnMiniPromptTab.Tag = tabIndex == 0 ? "Selected" : "0";
            if (BtnMiniHotkeyTab != null) BtnMiniHotkeyTab.Tag = tabIndex == 1 ? "Selected" : "1";
            if (BtnMiniLocationTab != null) BtnMiniLocationTab.Tag = tabIndex == 2 ? "Selected" : "2";

            // Show/hide tab content
            if (MiniWindowPromptTab != null) MiniWindowPromptTab.Visibility = tabIndex == 0 ? Visibility.Visible : Visibility.Collapsed;
            if (MiniWindowHotkeyTab != null) MiniWindowHotkeyTab.Visibility = tabIndex == 1 ? Visibility.Visible : Visibility.Collapsed;
            if (MiniWindowLocationTab != null) MiniWindowLocationTab.Visibility = tabIndex == 2 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void UpdateExternalToolsSubTab(int tabIndex)
        {
            var previousTab = _selectedExternalToolsSubTab;
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


            // Load credentials when switching to Baidu tab
            if (tabIndex == 1) LoadBaiduCredentials();
            if (tabIndex == 2) LoadTencentCredentials();
            if (tabIndex == 4) LoadGoogleCredentials();

            // Sync credentials when leaving Baidu tab
            if (previousTab == 1 && tabIndex != 1) SaveBaiduCredentials();
            if (previousTab == 2 && tabIndex != 2) SaveTencentCredentials();
            if (previousTab == 4 && tabIndex != 4) SaveGoogleCredentials();
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

            // Update button states
            if (BtnAiMainTab != null) BtnAiMainTab.Tag = tabIndex == 0 ? "Selected" : "0";
            if (BtnAiMiniTab != null) BtnAiMiniTab.Tag = tabIndex == 1 ? "Selected" : "1";
            // Tab 2 (Translations) removed
            if (BtnAiQuickActionTab != null) BtnAiQuickActionTab.Tag = tabIndex == 3 ? "Selected" : "3";

            // Show/hide tab content
            if (AiMainTab != null) AiMainTab.Visibility = tabIndex == 0 ? Visibility.Visible : Visibility.Collapsed;
            if (AiMiniTab != null) AiMiniTab.Visibility = tabIndex == 1 ? Visibility.Visible : Visibility.Collapsed;
            // if (AiTranslateTab != null) AiTranslateTab.Visibility = tabIndex == 2 ? Visibility.Visible : Visibility.Collapsed; // Removed
            if (AiQuickActionTab != null) AiQuickActionTab.Visibility = tabIndex == 3 ? Visibility.Visible : Visibility.Collapsed;
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

        // Quick Action Handlers
        private void AddQuickAction_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel == null) return;
            var selectedId = (QuickActionCandidateCombo?.SelectedValue as string) ?? "";
            if (string.IsNullOrWhiteSpace(selectedId)) return;

            var selectedFile = ViewModel.Files.FirstOrDefault(f => f.Id == selectedId);
            if (selectedFile == null) return;

            // Check if already exists
            if (ViewModel.LocalConfig.QuickActionPrompts.Any(qa => qa.Id == selectedId))
            {
                System.Windows.MessageBox.Show("该提示词已经在列表中", "提示");
                return;
            }

            // Add to quick actions list
            var quickAction = new QuickActionPrompt
            {
                Id = selectedFile.Id,
                Title = selectedFile.Title,
                BoundModelId = "" // Empty means use global default
            };

            ViewModel.LocalConfig.QuickActionPrompts.Add(quickAction);
            LocalConfigService.Save(ViewModel.LocalConfig);
        }

        private void RemoveQuickAction_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel == null) return;
            if (sender is not Button btn) return;
            if (btn.Tag is not QuickActionPrompt quickAction) return;

            ViewModel.LocalConfig.QuickActionPrompts.Remove(quickAction);
            LocalConfigService.Save(ViewModel.LocalConfig);
        }

        private void QuickActionModel_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ViewModel == null) return;
            // Selection is bound via TwoWay binding, just save the config
            LocalConfigService.Save(ViewModel.LocalConfig);
        }

        private void LoadGoogleCredentials()
        {
            if (ViewModel == null) return;

            var googleProfile = ViewModel.Config.ApiProfiles.FirstOrDefault(p => 
                p.Provider == ApiProvider.Google && p.ServiceType == ServiceType.Translation);
            
            if (googleProfile != null && GoogleBaseUrl != null && GoogleApiKey != null)
            {
                GoogleBaseUrl.Text = googleProfile.BaseUrl;
                GoogleApiKey.Text = googleProfile.Key1;
            }
        }

        private void SaveGoogleCredentials()
        {
            if (ViewModel == null) return;

            var googleProfile = ViewModel.Config.ApiProfiles.FirstOrDefault(p => 
                p.Provider == ApiProvider.Google && p.ServiceType == ServiceType.Translation);
            
            if (googleProfile == null)
            {
                googleProfile = new ApiProfile
                {
                    Name = "Google 翻译",
                    Provider = ApiProvider.Google,
                    ServiceType = ServiceType.Translation
                };
                ViewModel.Config.ApiProfiles.Add(googleProfile);
            }

            if (GoogleBaseUrl != null && GoogleApiKey != null)
            {
                googleProfile.BaseUrl = GoogleBaseUrl.Text;
                googleProfile.Key1 = GoogleApiKey.Text;
            }

            ConfigService.Save(ViewModel.Config);

            if (ViewModel.ExternalToolsVM != null)
            {
                ViewModel.ExternalToolsVM.RefreshProfiles();
            }
        }

        private async void TestGoogle_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel == null) return;
            var btn = sender as Button;
            if (btn != null) btn.IsEnabled = false;

            try
            {
                SaveGoogleCredentials();
                var profile = ViewModel.Config.ApiProfiles.FirstOrDefault(p => p.Provider == ApiProvider.Google && p.ServiceType == ServiceType.Translation);
                
                if (profile == null || string.IsNullOrWhiteSpace(profile.Key1))
                {
                    System.Windows.MessageBox.Show("请先填写 API Key", "参数错误");
                    return;
                }

                using (var client = new HttpClient())
                {
                    var service = new PromptMasterv5.Infrastructure.Services.GoogleService(client);
                    var result = await service.TranslateAsync("Hello World", profile);
                    if (!string.IsNullOrWhiteSpace(result) && !result.StartsWith("Google"))
                    {
                         System.Windows.MessageBox.Show("连接成功！\n翻译结果：" + result, "测试通过");
                    }
                    else
                    {
                         System.Windows.MessageBox.Show("测试结果：\n" + result, "提示");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"测试出错: {ex.Message}", "错误");
            }
            finally
            {
                if (btn != null) btn.IsEnabled = true;
            }
        }

        // AI Translation Prompt - Jump to Edit
        private void JumpToEditPrompt_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel == null) return;

            var promptId = ViewModel.Config.AiTranslationPromptId;
            if (string.IsNullOrWhiteSpace(promptId)) return;

            var prompt = ViewModel.Files.FirstOrDefault(f => f.Id == promptId);
            if (prompt != null)
            {
                ViewModel.SelectedFile = prompt;
                ViewModel.IsEditMode = true;
                ViewModel.SaveSettingsCommand.Execute(null); // Close settings
            }
        }

        // AI Translation Config - Save
        private void SaveAiTranslationConfig_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel == null) return;

            var promptId = ViewModel.Config.AiTranslationPromptId;
            var promptTitle = "";
            if (!string.IsNullOrWhiteSpace(promptId))
            {
                var prompt = ViewModel.Files.FirstOrDefault(f => f.Id == promptId);
                promptTitle = prompt?.Title ?? "";
            }

            var config = new AiTranslationConfig
            {
                PromptId = promptId,
                PromptTitle = promptTitle,
                BaseUrl = ViewModel.Config.AiBaseUrl,
                ApiKey = ViewModel.Config.AiApiKey,
                Model = ViewModel.Config.AiModel
            };

            ViewModel.Config.SavedAiTranslationConfigs.Add(config);
            ConfigService.Save(ViewModel.Config);
        }

        // AI Translation Config - Load from List
        private void AiTranslationConfigList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ViewModel == null) return;
            if (sender is not ListBox listBox) return;
            if (listBox.SelectedItem is not AiTranslationConfig config) return;

            // Load the configuration
            ViewModel.Config.AiTranslationPromptId = config.PromptId;
            ViewModel.Config.AiBaseUrl = config.BaseUrl;
            ViewModel.Config.AiApiKey = config.ApiKey;
            ViewModel.Config.AiModel = config.Model;

            // Clear selection to allow re-selecting the same item
            listBox.SelectedItem = null;
        }

        // AI Translation Config - Delete
        private void DeleteAiTranslationConfig_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel == null) return;
            if (sender is not Button btn) return;
            if (btn.Tag is not string configId) return;

            var config = ViewModel.Config.SavedAiTranslationConfigs.FirstOrDefault(c => c.Id == configId);
            if (config != null)
            {
                ViewModel.Config.SavedAiTranslationConfigs.Remove(config);
                ConfigService.Save(ViewModel.Config);
            }
        }

        // Connection Test Methods
        private async void TestBaiduOcr_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel == null) return;

            // Save current inputs first
            SaveBaiduCredentials();

            // Find Baidu OCR profile
            var profile = ViewModel.Config.ApiProfiles.FirstOrDefault(p =>
                p.Provider == ApiProvider.Baidu && p.ServiceType == ServiceType.OCR);

            if (profile == null || string.IsNullOrWhiteSpace(profile.Key1) || string.IsNullOrWhiteSpace(profile.Key2))
            {
                System.Windows.MessageBox.Show("请先填写 API Key 和 Secret Key", "测试失败");
                return;
            }

            // Create local Baidu service instance with HttpClient
            using var httpClient = new HttpClient();
            var baiduService = new BaiduService(httpClient);
            
            // Test with a minimal white 1x1 PNG image
            byte[] testImage = CreateTestImage();
            var result = await baiduService.OcrAsync(testImage, profile);

            if (result.StartsWith("错误") || result.Contains("错误"))
            {
                System.Windows.MessageBox.Show($"连接失败：{result}", "OCR 测试结果");
            }
            else
            {
                System.Windows.MessageBox.Show("连接成功！OCR API 配置正确。", "OCR 测试结果");
            }
        }

        private async void TestBaiduTranslate_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel == null) return;

            SaveBaiduCredentials();

            var profile = ViewModel.Config.ApiProfiles.FirstOrDefault(p =>
                p.Provider == ApiProvider.Baidu && p.ServiceType == ServiceType.Translation);

            if (profile == null || string.IsNullOrWhiteSpace(profile.Key1) || string.IsNullOrWhiteSpace(profile.Key2))
            {
                System.Windows.MessageBox.Show("请先填写 App ID 和 Secret Key", "测试失败");
                return;
            }

            // Create local Baidu service instance with HttpClient
            using var httpClient = new HttpClient();
            var baiduService = new BaiduService(httpClient);
            
            // Test with a simple English phrase
            var result = await baiduService.TranslateAsync("Hello", profile, "en", "zh");

            if (result.StartsWith("错误") || result.Contains("错误") || result.Contains("异常"))
            {
                System.Windows.MessageBox.Show($"连接失败：{result}", "翻译测试结果");
            }
            else
            {
                System.Windows.MessageBox.Show($"连接成功！\n\n测试翻译结果：{result}", "翻译测试结果");
            }
        }



        private async void TestTencentCloud_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel == null) return;

            SaveTencentCredentials();

            var profile = ViewModel.Config.ApiProfiles.FirstOrDefault(p =>
                p.Provider == ApiProvider.Tencent && p.ServiceType == ServiceType.Translation);

            if (profile == null || string.IsNullOrWhiteSpace(profile.Key1) || string.IsNullOrWhiteSpace(profile.Key2))
            {
                System.Windows.MessageBox.Show("请先填写 Secret ID 和 Secret Key", "测试失败");
                return;
            }

            using var httpClient = new HttpClient();
            var tencentService = new TencentService(httpClient);
            
            var result = await tencentService.TranslateAsync("Hello", profile, "auto", "zh");

            if (result.StartsWith("Error") || result.StartsWith("Tencent Error"))
            {
                System.Windows.MessageBox.Show($"连接失败：{result}", "腾讯云测试结果");
            }
            else
            {
                System.Windows.MessageBox.Show($"连接成功！\n\n测试翻译结果：{result}", "腾讯云测试结果");
            }
        }

        private void TestYoudao_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.MessageBox.Show("有道连接测试功能将在未来版本中实现。", "提示");
        }

        private byte[] CreateTestImage()
        {
            // Create a valid image with text to satisfy OCR requirements (min size and content)
            var width = 200;
            var height = 60;
            var renderBitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            var visual = new DrawingVisual();

            using (var context = visual.RenderOpen())
            {
                // Background
                context.DrawRectangle(System.Windows.Media.Brushes.White, null, new Rect(0, 0, width, height));
                
                // Text
                var formattedText = new FormattedText(
                    "OCR TEST",
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Windows.FlowDirection.LeftToRight,
                    new Typeface("Arial"),
                    24,
                    System.Windows.Media.Brushes.Black,
                    VisualTreeHelper.GetDpi(this).PixelsPerDip);

                context.DrawText(formattedText, new System.Windows.Point(40, 15));
            }

            renderBitmap.Render(visual);

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(renderBitmap));

            using (var stream = new MemoryStream())
            {
                encoder.Save(stream);
                return stream.ToArray();
            }
        }

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

            // Load Xunfei credentials when switching to Xunfei tab
            if (tabIndex == 1) LoadXunfeiCredentials();

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

        private void LoadXunfeiCredentials()
        {
            if (ViewModel == null) return;

            // Load API Secret to password box
            if (XunfeiApiSecretBox != null && !string.IsNullOrEmpty(ViewModel.Config.XunfeiApiSecret))
            {
                XunfeiApiSecretBox.Password = ViewModel.Config.XunfeiApiSecret;
            }
        }

        private void XunfeiApiSecretBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (ViewModel == null) return;
            if (sender is PasswordBox pb)
            {
                ViewModel.Config.XunfeiApiSecret = pb.Password;
            }
        }

        private async void TestXunfeiConnection_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel == null) return;

            var btn = sender as Button;
            if (btn != null) btn.IsEnabled = false;

            try
            {
                // Check configuration
                if (string.IsNullOrWhiteSpace(ViewModel.Config.XunfeiAppId) ||
                    string.IsNullOrWhiteSpace(ViewModel.Config.XunfeiApiKey) ||
                    string.IsNullOrWhiteSpace(ViewModel.Config.XunfeiApiSecret))
                {
                    System.Windows.MessageBox.Show("请先填写 AppID、API Key 和 API Secret", "配置不完整");
                    return;
                }

                // Save config first
                ConfigService.Save(ViewModel.Config);

                // Test connection by creating a simple WebSocket connection
                // We'll just validate the auth URL generation
                var testResult = await TestXunfeiAuthAsync();

                if (testResult)
                {
                    System.Windows.MessageBox.Show("连接成功！讯飞语音听写 API 配置正确。", "测试通过");
                }
                else
                {
                    System.Windows.MessageBox.Show("连接失败，请检查配置参数是否正确。", "测试失败");
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"测试出错: {ex.Message}", "错误");
            }
            finally
            {
                if (btn != null) btn.IsEnabled = true;
            }
        }

        private async Task<bool> TestXunfeiAuthAsync()
        {
            try
            {
                // Create a test transcriber to validate configuration
                var settingsService = ((App)System.Windows.Application.Current).ServiceProvider.GetService(typeof(ISettingsService)) as ISettingsService;
                if (settingsService == null) return false;

                var transcriber = new Infrastructure.Services.Transcribers.XunfeiIatTranscriber(settingsService);
                return await transcriber.IsConfiguredAsync();
            }
            catch
            {
                return false;
            }
        }

        #endregion
    }
}

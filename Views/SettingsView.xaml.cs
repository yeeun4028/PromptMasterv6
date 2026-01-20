using System;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
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

        public SettingsView()
        {
            InitializeComponent();
            Loaded += SettingsView_Loaded;
        }

        private void SettingsView_Loaded(object sender, RoutedEventArgs e)
        {
            // Initialize external tools sub-tab to Main tab
            UpdateExternalToolsSubTab(0);
            
            // Load Baidu credentials from AppConfig
            LoadBaiduCredentials();
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
                ViewModel.Config.SelectedTextTranslateHotkey = sb.ToString();
                tb.GetBindingExpression(TextBox.TextProperty)?.UpdateTarget();
                ViewModel.UpdateExternalToolsHotkeys();
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

        private async void TestAiConnection_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel == null) return;

            var btn = sender as Button;
            if (btn == null) return;

            var statusText = this.FindName("AiChatTestStatusText") as TextBlock ?? this.FindName("TestStatusText") as TextBlock;
            if (statusText == null) return;

            try
            {
                statusText.Text = "🔄 测试中...";
                statusText.Foreground = new SolidColorBrush(Color.FromRgb(102, 102, 102));
                btn.IsEnabled = false;

                (bool success, string message) = await ViewModel.TestAiConnectionAsync();

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

        private async void TestAiTranslationConnection_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel == null) return;

            var btn = sender as Button;
            if (btn == null) return;

            var statusText = this.FindName("TestAiTranslationStatusText") as TextBlock;
            if (statusText == null) return;

            try
            {
                statusText.Text = "🔄 测试中...";
                statusText.Foreground = new SolidColorBrush(Color.FromRgb(102, 102, 102));
                btn.IsEnabled = false;

                (bool success, string message) = await ViewModel.TestAiTranslationConnectionAsync();

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
            var previousTab = _selectedExternalToolsSubTab;
            _selectedExternalToolsSubTab = tabIndex;

            // Update button states directly - much simpler!
            if (BtnMainTab != null) BtnMainTab.Tag = tabIndex == 0 ? "Selected" : "0";
            if (BtnBaiduTab != null) BtnBaiduTab.Tag = tabIndex == 1 ? "Selected" : "1";
            if (BtnTencentTab != null) BtnTencentTab.Tag = tabIndex == 2 ? "Selected" : "2";
            if (BtnYoudaoTab != null) BtnYoudaoTab.Tag = tabIndex == 3 ? "Selected" : "3";
            if (BtnGoogleTab != null) BtnGoogleTab.Tag = tabIndex == 4 ? "Selected" : "4";
            if (BtnAiTab != null) BtnAiTab.Tag = tabIndex == 5 ? "Selected" : "5";

            // Show/hide tab content
            if (ExternalToolsMainTab != null) ExternalToolsMainTab.Visibility = tabIndex == 0 ? Visibility.Visible : Visibility.Collapsed;
            if (ExternalToolsBaiduTab != null) ExternalToolsBaiduTab.Visibility = tabIndex == 1 ? Visibility.Visible : Visibility.Collapsed;
            if (ExternalToolsTencentTab != null) ExternalToolsTencentTab.Visibility = tabIndex == 2 ? Visibility.Visible : Visibility.Collapsed;
            if (ExternalToolsYoudaoTab != null) ExternalToolsYoudaoTab.Visibility = tabIndex == 3 ? Visibility.Visible : Visibility.Collapsed;
            if (ExternalToolsGoogleTab != null) ExternalToolsGoogleTab.Visibility = tabIndex == 4 ? Visibility.Visible : Visibility.Collapsed;
            if (ExternalToolsAITab != null) ExternalToolsAITab.Visibility = tabIndex == 5 ? Visibility.Visible : Visibility.Collapsed;

            // Load credentials when switching to Baidu tab
            if (tabIndex == 1) LoadBaiduCredentials();
            if (tabIndex == 4) LoadGoogleCredentials();

            // Sync credentials when leaving Baidu tab
            if (previousTab == 1 && tabIndex != 1) SaveBaiduCredentials();
            if (previousTab == 4 && tabIndex != 4) SaveGoogleCredentials();
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



        private void TestTencentCloud_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.MessageBox.Show("腾讯云连接测试功能将在未来版本中实现。", "提示");
        }

        private void TestYoudao_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.MessageBox.Show("有道连接测试功能将在未来版本中实现。", "提示");
        }

        private byte[] CreateTestImage()
        {
            // Create a minimal 1x1 white PNG image for testing OCR
            return Convert.FromBase64String(
                "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8DwHwAFBQIAX8jx0gAAAABJRU5ErkJggg=="
            );
        }
    }
}

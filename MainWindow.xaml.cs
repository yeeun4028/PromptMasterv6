using System;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Text.RegularExpressions;
using PromptMasterv5.ViewModels;
using System.Text;
using System.Windows.Media;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using PromptMasterv5.Models;
using PromptMasterv5.Services;
using System.Windows.Forms;
using System.Windows.Threading;
using System.Linq; // 新增引用，用于查询子窗口状态
using System.Windows.Controls.Primitives;
using System.Windows.Shapes;
using System.Windows.Documents;

// 引用自定义枚举和控件别名，解决命名冲突
using InputMode = PromptMasterv5.Models.InputMode;
using Button = System.Windows.Controls.Button;
using TextBox = System.Windows.Controls.TextBox;
using ListBox = System.Windows.Controls.ListBox;
using RichTextBox = System.Windows.Controls.RichTextBox;
using WpfControl = System.Windows.Controls.Control;
using WinFormsCursor = System.Windows.Forms.Cursor;
using MessageBox = System.Windows.MessageBox;
using Application = System.Windows.Application;

namespace PromptMasterv5
{
    public partial class MainWindow : Window
    {
        public MainViewModel ViewModel { get; }

        private double _lastFullWidth = 1000;
        private double _lastFullHeight = 600;
        private double _lastFullLeft = 100;
        private double _lastFullTop = 100;

        private DateTime _lastMiniEnterTime = DateTime.MinValue;
        private DateTime _lastVarEnterTime = DateTime.MinValue;
        private DateTime _lastAddEnterTime = DateTime.MinValue;
        private int _miniEnterSequence = 0;

        private System.Windows.Forms.NotifyIcon? _notifyIcon;
        private bool _isExiting = false;
        private DispatcherTimer? _hideTimer;
        private DispatcherTimer? _miniPersistTimer;

        private const int MiniMaxAutoLines = 23;

        private double _miniDefaultHeight = 0;
        private double _miniLineHeight = 25.6;
        private double? _miniBottomAnchor;
        private DispatcherTimer? _miniAutoResizeTimer;
        private bool _isApplyingMiniAutoResize = false;
        private bool _isUpdatingMiniInputDocument = false;

        private double GetMiniDefaultWidth()
        {
            if (ViewModel == null) return 500;
            var w = ViewModel.LocalConfig.MiniDefaultWidth;
            if (w <= 0) w = 500;
            if (w < 200) w = 200;
            return w;
        }

        private double GetMiniExpandedWidth()
        {
            if (ViewModel == null) return 800;
            var w = ViewModel.LocalConfig.MiniExpandedWidth;
            if (w <= 0) w = 800;
            var min = GetMiniDefaultWidth();
            if (w < min) w = min;
            return w;
        }

        private string GetMiniUserInputText()
        {
            if (MiniInputBox?.Document == null) return "";
            var paragraphs = MiniInputBox.Document.Blocks.OfType<Paragraph>().ToList();
            if (paragraphs.Count == 0) return "";

            var selected = GetMiniSelectedPrompt();
            var startIndex = (selected != null && paragraphs.Count >= 2) ? 1 : 0;

            var sb = new StringBuilder();
            for (int i = startIndex; i < paragraphs.Count; i++)
            {
                var p = paragraphs[i];
                var segment = new TextRange(p.ContentStart, p.ContentEnd).Text ?? "";
                segment = segment.TrimEnd('\r', '\n');

                if (sb.Length > 0) sb.Append('\n');
                sb.Append(segment);
            }

            return sb.ToString();
        }

        private PromptItem? GetMiniSelectedPrompt()
        {
            if (ViewModel == null) return null;
            var id = ViewModel.LocalConfig.MiniSelectedPinnedPromptId ?? "";
            if (string.IsNullOrWhiteSpace(id)) return null;
            return ViewModel.Files.FirstOrDefault(f => f.Id == id);
        }

        private void RebuildMiniInputDocument(string userText, bool focusUserInput)
        {
            if (MiniInputBox == null) return;
            if (ViewModel == null) return;

            _isUpdatingMiniInputDocument = true;
            try
            {
                var doc = new FlowDocument
                {
                    PagePadding = new Thickness(0),
                    FontFamily = MiniInputBox.FontFamily,
                    FontSize = MiniInputBox.FontSize,
                    LineHeight = MiniInputBox.FontSize * 1.6,
                    LineStackingStrategy = LineStackingStrategy.BlockLineHeight,
                    Background = System.Windows.Media.Brushes.Transparent
                };

                var selected = GetMiniSelectedPrompt();
                if (selected != null)
                {
                    var chipParagraph = new Paragraph { Margin = new Thickness(0, 2, 0, 3) };
                    var showIcons = ViewModel.LocalConfig.MiniPinnedPromptShowIcons && !string.IsNullOrWhiteSpace(selected.IconGeometry);

                    if (showIcons)
                    {
                        Geometry? geometry = null;
                        try { geometry = Geometry.Parse(selected.IconGeometry!); } catch { }

                        if (geometry != null)
                        {
                            var path = new Path
                            {
                                Data = geometry,
                                Width = 14,
                                Height = 14,
                                Stretch = Stretch.Uniform,
                                Fill = TryFindResource("MiniModeBtnActiveBrush") as System.Windows.Media.Brush ?? System.Windows.Media.Brushes.White,
                                VerticalAlignment = VerticalAlignment.Center
                            };
                            var border = new Border
                            {
                                Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x33, 0x33, 0x33)),
                                BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x65, 0x65, 0x65)),
                                BorderThickness = new Thickness(1),
                                CornerRadius = new CornerRadius(3),
                                Padding = new Thickness(1.5)
                            };
                            border.Child = path;
                            chipParagraph.Inlines.Add(new InlineUIContainer(border) { BaselineAlignment = BaselineAlignment.Center });
                        }
                        else
                        {
                            var border = new Border
                            {
                                Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x33, 0x33, 0x33)),
                                BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x65, 0x65, 0x65)),
                                BorderThickness = new Thickness(1),
                                CornerRadius = new CornerRadius(3),
                                Padding = new Thickness(1.5)
                            };
                            border.Child = new TextBlock
                            {
                                Text = selected.Title ?? "",
                                Foreground = TryFindResource("MiniModeBtnActiveBrush") as System.Windows.Media.Brush ?? System.Windows.Media.Brushes.White,
                                FontFamily = new System.Windows.Media.FontFamily("Segoe UI, Microsoft YaHei UI, Microsoft YaHei"),
                                FontSize = 14,
                                FontWeight = FontWeights.Normal
                            };
                            chipParagraph.Inlines.Add(new InlineUIContainer(border) { BaselineAlignment = BaselineAlignment.Center });
                        }
                    }
                    else
                    {
                        var border = new Border
                        {
                            Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x33, 0x33, 0x33)),
                            BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x65, 0x65, 0x65)),
                            BorderThickness = new Thickness(1),
                            CornerRadius = new CornerRadius(3),
                            Padding = new Thickness(1.5)
                        };
                        border.Child = new TextBlock
                        {
                            Text = selected.Title ?? "",
                            Foreground = TryFindResource("MiniModeBtnActiveBrush") as System.Windows.Media.Brush ?? System.Windows.Media.Brushes.White,
                            FontFamily = new System.Windows.Media.FontFamily("Segoe UI, Microsoft YaHei UI, Microsoft YaHei"),
                            FontSize = 14,
                            FontWeight = FontWeights.Normal
                        };
                        chipParagraph.Inlines.Add(new InlineUIContainer(border) { BaselineAlignment = BaselineAlignment.Center });
                    }

                    doc.Blocks.Add(chipParagraph);
                }

                Paragraph? firstUserParagraph = null;
                var normalized = (userText ?? "").Replace("\r\n", "\n").Replace("\r", "\n");
                var lines = normalized.Split('\n');
                foreach (var line in lines)
                {
                    var p = new Paragraph { Margin = new Thickness(0) };
                    p.Inlines.Add(new Run(line));
                    doc.Blocks.Add(p);
                    firstUserParagraph ??= p;
                }

                if (firstUserParagraph == null)
                {
                    firstUserParagraph = new Paragraph { Margin = new Thickness(0) };
                    doc.Blocks.Add(firstUserParagraph);
                }

                MiniInputBox.Document = doc;

                if (focusUserInput)
                {
                    MiniInputBox.Focus();
                    MiniInputBox.CaretPosition = firstUserParagraph.ContentStart;
                }
            }
            finally
            {
                _isUpdatingMiniInputDocument = false;
            }
        }

        private int GetMiniVisualLineCount()
        {
            if (MiniInputBox == null) return 1;

            MiniInputBox.UpdateLayout();
            if (_miniLineHeight > 0)
            {
                var extent = MiniInputBox.ExtentHeight;
                if (extent > 0)
                {
                    var linesByExtent = (int)Math.Ceiling(extent / _miniLineHeight);
                    if (linesByExtent < 1) linesByExtent = 1;
                    return linesByExtent;
                }
            }

            if (MiniInputBox.Document == null) return 1;
            var start = MiniInputBox.Document.ContentStart;
            if (start == null) return 1;
            var line = start;
            var count = 1;
            while (true)
            {
                var next = line.GetLineStartPosition(1);
                if (next == null) break;
                count++;
                line = next;
                if (count > 500) break;
            }
            return count;
        }

        public MainWindow()
        {
            InitializeComponent();

            // 设置窗口不在任务栏显示
            this.ShowInTaskbar = false;

            ViewModel = new MainViewModel();
            this.DataContext = ViewModel;

            ViewModel.PropertyChanged += ViewModel_PropertyChanged;
            InitializeTrayIcon();
            ApplyModeState();
            LocationChanged += MainWindow_LocationChanged;
            SizeChanged += MainWindow_SizeChanged;

            // 处理窗口关闭事件
            this.Closing += MainWindow_Closing;
        }

        private void ScheduleMiniPersist()
        {
            if (ViewModel == null) return;
            if (ViewModel.IsFullMode) return;

            if (_miniPersistTimer == null)
            {
                _miniPersistTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
                _miniPersistTimer.Tick += (s, e) =>
                {
                    _miniPersistTimer.Stop();
                    LocalConfigService.Save(ViewModel.LocalConfig);
                };
            }
            _miniPersistTimer.Stop();
            _miniPersistTimer.Start();
        }

        private void MainWindow_LocationChanged(object? sender, EventArgs e)
        {
            if (ViewModel == null) return;
            if (ViewModel.IsFullMode) return;
            if (WindowState != WindowState.Normal) return;

            ViewModel.LocalConfig.MiniWindowLeft = Left;
            ViewModel.LocalConfig.MiniWindowTop = Top;
            _miniBottomAnchor = Top + Height;
            ScheduleMiniPersist();
        }

        private void MainWindow_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (ViewModel == null) return;
            if (ViewModel.IsFullMode) return;
            if (WindowState != WindowState.Normal) return;

            _miniBottomAnchor = Top + Height;
            if (!_isApplyingMiniAutoResize)
            {
                ViewModel.LocalConfig.MiniWindowWidth = Width;
                ViewModel.LocalConfig.MiniWindowHeight = Height;
                ScheduleMiniPersist();
            }
        }

        private void InitializeTrayIcon()
        {
            try
            {
                _notifyIcon = new System.Windows.Forms.NotifyIcon();
                
                try
                {
                    _notifyIcon.Icon = System.Drawing.Icon.ExtractAssociatedIcon(System.Windows.Forms.Application.ExecutablePath);
                }
                catch
                {
                    try
                    {
                        if (System.IO.File.Exists("pro_icon.ico"))
                            _notifyIcon.Icon = new System.Drawing.Icon("pro_icon.ico");
                        else
                            _notifyIcon.Icon = System.Drawing.SystemIcons.Application;
                    }
                    catch
                    {
                        _notifyIcon.Icon = System.Drawing.SystemIcons.Application;
                    }
                }

                _notifyIcon.Text = "PromptMaster v5";
                _notifyIcon.Visible = true;

                // 添加托盘菜单
                var contextMenu = new System.Windows.Forms.ContextMenuStrip();
                contextMenu.Items.Add("显示/隐藏窗口", null, (s, e) => ToggleWindowVisibility());
                contextMenu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
                contextMenu.Items.Add("退出", null, (s, e) =>
                {
                    _isExiting = true;
                    this.Close();
                });

                _notifyIcon.ContextMenuStrip = contextMenu;

                // 单击托盘图标显示/隐藏窗口
                _notifyIcon.Click += (s, e) =>
                {
                    if (e is System.Windows.Forms.MouseEventArgs mouseArgs && mouseArgs.Button == System.Windows.Forms.MouseButtons.Left)
                    {
                        ToggleWindowVisibility();
                    }
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"托盘图标初始化失败: {ex.Message}");
            }
        }

        private void ToggleWindowVisibility()
        {
            if (this.Visibility == Visibility.Visible)
            {
                this.Hide();
            }
            else
            {
                // 取消隐藏定时器
                StopHideTimer();

                if (ViewModel != null && !ViewModel.IsFullMode && ViewModel.LocalConfig.MiniUseDefaultPosition)
                {
                    var cfg = ViewModel.LocalConfig;
                    Left = cfg.MiniDefaultLeft;
                    if (cfg.MiniDefaultBottom > 0)
                    {
                        _miniBottomAnchor = cfg.MiniDefaultBottom;
                        Top = _miniBottomAnchor.Value - Height;
                    }
                }

                this.Show();
                this.Activate();
                this.Focus();

                // 唤醒时强制置顶
                this.Topmost = true;
                NativeMethods.SetForegroundWindow(new System.Windows.Interop.WindowInteropHelper(this).Handle);
            }
        }

        private void MainWindow_Closing(object? sender, CancelEventArgs e)
        {
            // ★★★ 新增：安全退出拦截机制 ★★★
            if (ViewModel.IsDirty)
            {
                var result = MessageBox.Show("您有未备份的修改。是否在退出/隐藏前备份到云端？", "未保存的更改", MessageBoxButton.YesNoCancel, MessageBoxImage.Warning);
                
                if (result == MessageBoxResult.Cancel)
                {
                    e.Cancel = true; // 取消关闭/隐藏操作
                    return;
                }
                else if (result == MessageBoxResult.Yes)
                {
                    // 同步执行备份
                    ViewModel.ManualBackupCommand.Execute(null);
                }
                // 如果选 No，直接继续执行后续关闭或隐藏操作
            }

            // 如果不是通过退出菜单关闭，则隐藏窗口而不是关闭
            if (!_isExiting)
            {
                e.Cancel = true;  // 取消关闭事件
                this.Hide();       // 隐藏窗口
                return;
            }

            // 清理托盘图标
            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
            }
        }

        private void Window_Activated(object sender, EventArgs e)
        {
            // 1. 窗口被激活（唤醒）时，取消任何待执行的隐藏操作
            StopHideTimer();

            // 2. 需求实现：唤醒时置顶显示
            // 无论是极简还是完整模式，只要被激活，就应该在最上层
            this.Topmost = true;

            // 3. 强制抢占前台焦点 (Win32 API)
            NativeMethods.SetForegroundWindow(new System.Windows.Interop.WindowInteropHelper(this).Handle);

            // 4. 处理极简模式下的输入框焦点
            if (ViewModel != null && !ViewModel.IsFullMode)
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (MiniInputBox != null)
                    {
                        MiniInputBox.Focus();
                        Keyboard.Focus(MiniInputBox);
                        if (MiniInputBox.Document != null)
                        {
                            MiniInputBox.CaretPosition = MiniInputBox.Document.ContentEnd;
                            MiniInputBox.ScrollToEnd();
                        }
                    }
                }), System.Windows.Threading.DispatcherPriority.ContextIdle);
            }
        }

        private void Window_Deactivated(object sender, EventArgs e)
        {
            if (ViewModel != null && ViewModel.LocalConfig.IsMiniTopmostLocked)
            {
                return;
            }

            // 1. 需求实现：不需要始终保持置顶
            // 当失去焦点（用户点击了其他软件）时，取消置顶，允许其他窗口覆盖它
            this.Topmost = false;

            // 取消之前的定时器
            StopHideTimer();

            // 2. 需求实现：失去焦点自动隐藏
            // 创建新的定时器，延迟200毫秒后检查并隐藏窗口
            // 延迟是为了防止在应用内部切换焦点（如点击弹出菜单、对话框）时误判为失焦
            _hideTimer = new DispatcherTimer();
            _hideTimer.Interval = TimeSpan.FromMilliseconds(200);
            _hideTimer.Tick += (s, args) =>
            {
                StopHideTimer();

                // ★★★ 核心修复：检测子窗口状态 ★★★
                // 如果当前应用程序的任何窗口（包括对话框）处于激活状态，则视为未完全失焦，不隐藏
                bool isAnyChildActive = Application.Current.Windows.Cast<Window>().Any(w => w.IsActive);

                // 只有当主窗口不活动，且没有任何子窗口活动时，才执行隐藏
                if (!this.IsActive && !isAnyChildActive)
                {
                    this.Hide();
                }
            };
            _hideTimer.Start();
        }

        private void StopHideTimer()
        {
            if (_hideTimer != null)
            {
                _hideTimer.Stop();
                _hideTimer = null;
            }
        }

        private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MainViewModel.IsFullMode))
            {
                ApplyModeState();
            }
            else if (e.PropertyName == nameof(MainViewModel.MiniInputText))
            {
                if (string.IsNullOrEmpty(ViewModel.MiniInputText) && !ViewModel.IsFullMode)
                {
                    _ = Dispatcher.BeginInvoke(new Action(() =>
                    {
                        ResetMiniWindowToDefaultSize();
                    }), System.Windows.Threading.DispatcherPriority.ContextIdle);
                }
                else if (!ViewModel.IsFullMode && !_isUpdatingMiniInputDocument && MiniInputBox != null && !MiniInputBox.IsKeyboardFocusWithin)
                {
                    RebuildMiniInputDocument(ViewModel.MiniInputText ?? "", focusUserInput: false);
                }
            }
        }

        private void ResetMiniWindowToDefaultSize()
        {
            if (ViewModel.IsFullMode) return;
            if (_miniDefaultHeight <= 0)
            {
                EnsureMiniDefaultSizeMeasured();
                return;
            }

            var bottom = _miniBottomAnchor ?? (Top + Height);
            var targetHeight = _miniDefaultHeight;

            _isApplyingMiniAutoResize = true;
            Width = GetMiniDefaultWidth();
            Height = targetHeight;
            Top = bottom - Height;
            _isApplyingMiniAutoResize = false;

            _miniBottomAnchor = bottom;
            ApplyMiniScrollBarAppearance(isOverflow: false);
        }

        private void ApplyModeState()
        {
            if (ViewModel.IsFullMode)
            {
                this.Width = _lastFullWidth;
                this.Height = _lastFullHeight;
                this.Left = _lastFullLeft;
                this.Top = _lastFullTop;
                this.ResizeMode = ResizeMode.CanResize;

                // 切换模式时，确保重置Topmost
                this.Topmost = true;
                EnsureWindowOnScreen();
            }
            else
            {
                _lastFullWidth = this.Width;
                _lastFullHeight = this.Height;
                _lastFullLeft = this.Left;
                _lastFullTop = this.Top;

                var cfg = ViewModel.LocalConfig;
                this.Width = GetMiniDefaultWidth();
                if (cfg.MiniUseDefaultPosition)
                {
                    this.Left = cfg.MiniDefaultLeft;
                    var bottom = cfg.MiniDefaultBottom > 0 ? cfg.MiniDefaultBottom : (cfg.MiniWindowTop + this.Height);
                    _miniBottomAnchor = bottom;
                }
                else
                {
                    this.Left = cfg.MiniWindowLeft;
                    this.Top = cfg.MiniWindowTop;
                }
                if (this.Height < 160) this.Height = 160;
                if (cfg.MiniUseDefaultPosition && _miniBottomAnchor.HasValue) this.Top = _miniBottomAnchor.Value - this.Height;

                this.ResizeMode = ResizeMode.NoResize;

                // 切换模式时，确保重置Topmost并激活
                this.Topmost = true;
                EnsureWindowOnScreen();
                this.Activate();
                NativeMethods.SetForegroundWindow(new System.Windows.Interop.WindowInteropHelper(this).Handle);

                EnsureMiniDefaultSizeMeasured();
                RebuildMiniInputDocument(ViewModel.MiniInputText ?? "", focusUserInput: true);
            }
        }

        private void EnsureMiniDefaultSizeMeasured()
        {
            _ = Dispatcher.BeginInvoke(new Action(() =>
            {
                if (ViewModel.IsFullMode) return;
                if (MiniInputBox == null) return;

                var lineHeightObj = MiniInputBox.GetValue(TextBlock.LineHeightProperty);
                _miniLineHeight = lineHeightObj is double lh && lh > 0 ? lh : 25.6;
                var padding = MiniInputBox.Padding;

                var modeBarHeight = MiniModeBar?.ActualHeight ?? 0;
                if (modeBarHeight < 12) modeBarHeight = 12;

                var overhead = ActualHeight - MiniInputBox.ActualHeight - modeBarHeight;
                if (overhead < 40) overhead = 40;

                _miniDefaultHeight = overhead + modeBarHeight + (_miniLineHeight + padding.Top + padding.Bottom);
                if (_miniDefaultHeight < 70) _miniDefaultHeight = 70;

                var cfg = ViewModel.LocalConfig;
                var bottom = _miniBottomAnchor ?? (Top + Height);
                if (cfg.MiniUseDefaultPosition && cfg.MiniDefaultBottom > 0) bottom = cfg.MiniDefaultBottom;

                Height = _miniDefaultHeight;
                Width = GetMiniDefaultWidth();
                Top = bottom - Height;
                _miniBottomAnchor = bottom;
                ApplyMiniScrollBarAppearance(isOverflow: false);
            }), DispatcherPriority.Loaded);
        }

        private void ApplyMiniScrollBarAppearance(bool isOverflow)
        {
            if (MiniInputBox == null) return;
            var styleKey = "MiniInvisibleScrollBarStyle";
            if (TryFindResource(styleKey) is Style style)
            {
                MiniInputBox.Resources[typeof(System.Windows.Controls.Primitives.ScrollBar)] = style;
            }
        }

        private void ScheduleMiniAutoResize()
        {
            if (ViewModel.IsFullMode) return;
            if (MiniInputBox == null) return;

            if (_miniAutoResizeTimer == null)
            {
                _miniAutoResizeTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(80) };
                _miniAutoResizeTimer.Tick += (s, e) =>
                {
                    _miniAutoResizeTimer.Stop();
                    ApplyMiniAutoResize();
                };
            }

            _miniAutoResizeTimer.Stop();
            _miniAutoResizeTimer.Start();
        }

        private void ApplyMiniAutoResize()
        {
            if (ViewModel.IsFullMode) return;
            if (MiniInputBox == null) return;
            if (_miniDefaultHeight <= 0) return;

            var lineCount = GetMiniVisualLineCount();
            if (lineCount < 1) lineCount = 1;
            var isOverflow = lineCount > MiniMaxAutoLines;

            var targetWidth = isOverflow ? GetMiniExpandedWidth() : GetMiniDefaultWidth();
            var cappedLines = Math.Min(lineCount, MiniMaxAutoLines);
            var targetHeight = _miniDefaultHeight + (cappedLines - 1) * _miniLineHeight;

            var bottom = _miniBottomAnchor ?? (Top + Height);
            var workAreaTop = SystemParameters.WorkArea.Top;
            var maxHeightByScreen = bottom - workAreaTop;
            if (targetHeight > maxHeightByScreen) targetHeight = maxHeightByScreen;
            if (targetHeight < _miniDefaultHeight) targetHeight = _miniDefaultHeight;

            _isApplyingMiniAutoResize = true;
            Width = targetWidth;
            Height = targetHeight;
            Top = bottom - Height;
            _isApplyingMiniAutoResize = false;

            ApplyMiniScrollBarAppearance(isOverflow);
        }

        private void MiniInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isUpdatingMiniInputDocument) return;

            if (ViewModel != null && !ViewModel.IsFullMode)
            {
                ViewModel.MiniInputText = GetMiniUserInputText();
            }

            if (MiniInputBox != null)
            {
                var text = GetMiniUserInputText().Trim();
                var isUrlLike = Regex.IsMatch(text, @"^(https?://|www\.)\S+$", RegexOptions.IgnoreCase);
                if (!isUrlLike && Uri.TryCreate(text, UriKind.Absolute, out var uri))
                {
                    isUrlLike = uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps;
                }

                if (isUrlLike)
                {
                    var accent = TryFindResource("AccentBrush") as System.Windows.Media.Brush;
                    MiniInputBox.Foreground = accent ?? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x41, 0x83, 0xC4));
                }
                else
                {
                    MiniInputBox.ClearValue(WpfControl.ForegroundProperty);
                }
            }

            if (ViewModel != null && !ViewModel.IsFullMode)
            {
                ScheduleMiniAutoResize();
            }
        }

        private childItem? FindVisualChild<childItem>(DependencyObject? obj) where childItem : DependencyObject
        {
            if (obj == null) return null;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(obj); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(obj, i);
                if (child is childItem item) return item;
                childItem? childOfChild = FindVisualChild<childItem>(child);
                if (childOfChild != null) return childOfChild;
            }
            return null;
        }

        private void EnsureWindowOnScreen()
        {
            if (this.Left < 0) this.Left = 0;
            if (this.Top < 0) this.Top = 0;
        }

        private void MiniWindow_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) { if (e.ButtonState == MouseButtonState.Pressed) this.DragMove(); }
        private void FullWindow_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) { if (e.ButtonState == MouseButtonState.Pressed) this.DragMove(); }

        private void MiniPinnedPromptItem_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel == null) return;
            if (ViewModel.IsFullMode) return;

            if (sender is not Button btn) return;
            var id = btn.Tag as string ?? "";
            if (string.IsNullOrWhiteSpace(id)) return;

            var userText = ViewModel.MiniInputText ?? "";
            if (ViewModel.LocalConfig.MiniSelectedPinnedPromptId == id)
            {
                ViewModel.LocalConfig.MiniSelectedPinnedPromptId = "";
            }
            else
            {
                ViewModel.LocalConfig.MiniSelectedPinnedPromptId = id;
                if (ViewModel.LocalConfig.MiniPinnedPromptClickShowsFullContent)
                {
                    var p = ViewModel.Files.FirstOrDefault(f => f.Id == id);
                    userText = p?.Content ?? "";
                    ViewModel.MiniInputText = userText;
                }
            }

            RebuildMiniInputDocument(userText, focusUserInput: true);
        }

        private void MiniResizeThumb_DragDelta(object sender, DragDeltaEventArgs e)
        {
            if (ViewModel == null) return;
            if (ViewModel.IsFullMode) return;
            if (WindowState != WindowState.Normal) return;
            if (sender is not FrameworkElement fe) return;
            if (fe.Tag is not string tag) return;

            const double minWidth = 260;
            const double minHeight = 90;

            double newLeft = Left;
            double newTop = Top;
            double newWidth = Width;
            double newHeight = Height;

            bool resizeLeft = tag.Contains("Left", StringComparison.OrdinalIgnoreCase) && !tag.Equals("Top", StringComparison.OrdinalIgnoreCase) && !tag.Equals("Bottom", StringComparison.OrdinalIgnoreCase);
            bool resizeRight = tag.Contains("Right", StringComparison.OrdinalIgnoreCase);
            bool resizeTop = tag.Contains("Top", StringComparison.OrdinalIgnoreCase);
            bool resizeBottom = tag.Contains("Bottom", StringComparison.OrdinalIgnoreCase) && !tag.Equals("Top", StringComparison.OrdinalIgnoreCase);

            if (tag == "Left") { resizeLeft = true; resizeTop = false; resizeBottom = false; resizeRight = false; }
            if (tag == "Right") { resizeRight = true; resizeTop = false; resizeBottom = false; resizeLeft = false; }
            if (tag == "Top") { resizeTop = true; resizeLeft = false; resizeRight = false; resizeBottom = false; }
            if (tag == "Bottom") { resizeBottom = true; resizeLeft = false; resizeRight = false; resizeTop = false; }

            if (resizeLeft)
            {
                double proposedWidth = newWidth - e.HorizontalChange;
                if (proposedWidth >= minWidth)
                {
                    newWidth = proposedWidth;
                    newLeft += e.HorizontalChange;
                }
                else
                {
                    newLeft += newWidth - minWidth;
                    newWidth = minWidth;
                }
            }
            else if (resizeRight)
            {
                newWidth = Math.Max(minWidth, newWidth + e.HorizontalChange);
            }

            if (resizeTop)
            {
                double proposedHeight = newHeight - e.VerticalChange;
                if (proposedHeight >= minHeight)
                {
                    newHeight = proposedHeight;
                    newTop += e.VerticalChange;
                }
                else
                {
                    newTop += newHeight - minHeight;
                    newHeight = minHeight;
                }
            }
            else if (resizeBottom)
            {
                newHeight = Math.Max(minHeight, newHeight + e.VerticalChange);
            }

            Left = newLeft;
            Top = newTop;
            Width = newWidth;
            Height = newHeight;
        }

        private async void MiniInput_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            var box = sender as RichTextBox;
            if (box == null) return;

            if (ViewModel != null && !ViewModel.IsFullMode && !_isUpdatingMiniInputDocument)
            {
                ViewModel.MiniInputText = GetMiniUserInputText();
            }

            if (e.Key == Key.Enter && (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
            {
                return;
            }

            if ((e.Key == Key.Delete || e.Key == Key.Back) && ViewModel != null && !ViewModel.IsFullMode)
            {
                var selectedId = ViewModel.LocalConfig.MiniSelectedPinnedPromptId ?? "";
                if (!string.IsNullOrWhiteSpace(selectedId) && box.Document != null)
                {
                    var userText = ViewModel.MiniInputText ?? GetMiniUserInputText();

                    var caret = box.CaretPosition.GetInsertionPosition(LogicalDirection.Forward);
                    var docStart = box.Document.ContentStart.GetInsertionPosition(LogicalDirection.Forward);

                    var paragraphs = box.Document.Blocks.OfType<Paragraph>().ToList();
                    var chipPara = paragraphs.Count > 1 ? paragraphs.First() : null;
                    var userParas = paragraphs.Skip(chipPara != null ? 1 : 0).ToList();
                    var firstUserPara = userParas.FirstOrDefault();

                    var atDocStart = caret != null && docStart != null && caret.CompareTo(docStart) == 0;
                    var atUserStart = firstUserPara != null && caret != null && caret.CompareTo(firstUserPara.ContentStart) == 0;
                    var inChipLine = chipPara != null && caret != null && caret.CompareTo(chipPara.ContentStart) >= 0 && caret.CompareTo(chipPara.ContentEnd) <= 0;

                    var shouldRemoveChip =
                        inChipLine ||
                        (e.Key == Key.Delete && atDocStart) ||
                        (e.Key == Key.Back && atUserStart);

                    if (shouldRemoveChip)
                    {
                        ViewModel.LocalConfig.MiniSelectedPinnedPromptId = "";
                        RebuildMiniInputDocument(userText, focusUserInput: true);
                        e.Handled = true;
                        return;
                    }
                }
            }

            if (e.Key == Key.Delete && (ViewModel.LocalConfig.MiniAiOnlyChatEnabled || ViewModel.IsAiResultDisplayed))
            {
                ViewModel.MiniInputText = "";
                ViewModel.IsAiResultDisplayed = false;
                RebuildMiniInputDocument("", focusUserInput: true);
                e.Handled = true;
                return;
            }

            if (ViewModel.IsSearchPopupOpen && ViewModel.SearchResults.Count > 0)
            {
                if (e.Key == Key.Down)
                {
                    int newIndex = SearchListBox.SelectedIndex + 1;
                    if (newIndex >= ViewModel.SearchResults.Count) newIndex = 0;
                    SearchListBox.SelectedIndex = newIndex;
                    SearchListBox.ScrollIntoView(SearchListBox.SelectedItem);
                    e.Handled = true;
                    return;
                }
                else if (e.Key == Key.Up)
                {
                    int newIndex = SearchListBox.SelectedIndex - 1;
                    if (newIndex < 0) newIndex = ViewModel.SearchResults.Count - 1;
                    SearchListBox.SelectedIndex = newIndex;
                    SearchListBox.ScrollIntoView(SearchListBox.SelectedItem);
                    e.Handled = true;
                    return;
                }
            }

            if (e.Key == Key.Enter)
            {
                if (ViewModel.IsSearchPopupOpen)
                {
                    ViewModel.ConfirmSearchResultCommand.Execute(null);
                    e.Handled = true;
                    await Dispatcher.BeginInvoke(new Action(() =>
                    {
                        if (ViewModel.HasVariables)
                        {
                            var container = MiniVarsList.ItemContainerGenerator.ContainerFromIndex(0) as FrameworkElement;
                            if (container != null)
                            {
                                var firstBox = FindVisualChild<TextBox>(container);
                                if (firstBox != null)
                                {
                                    firstBox.Focus();
                                    firstBox.SelectAll();
                                    return;
                                }
                            }
                        }
                        RebuildMiniInputDocument(ViewModel.MiniInputText ?? "", focusUserInput: true);
                    }), System.Windows.Threading.DispatcherPriority.ContextIdle);
                    return;
                }

                bool isCtrl = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;
                var now = DateTime.Now;
                var span = (now - _lastMiniEnterTime).TotalMilliseconds;

                if (isCtrl)
                {
                    e.Handled = true;
                    _miniEnterSequence++;
                    await TriggerSendProcess(InputMode.SmartFocus);
                    return;
                }

                if (!ViewModel.LocalConfig.MiniAiOnlyChatEnabled)
                {
                    e.Handled = true;
                    _miniEnterSequence++;
                    await TriggerSendProcess(ViewModel.LocalConfig.Mode);
                    return;
                }

                if (span < 500)
                {
                    e.Handled = true;
                    _lastMiniEnterTime = DateTime.MinValue;
                    _miniEnterSequence++;
                    await TriggerSendProcess(InputMode.CoordinateClick);
                    return;
                }

                e.Handled = true;
                _lastMiniEnterTime = now;
                var enterSeq = ++_miniEnterSequence;

                _ = Dispatcher.InvokeAsync(async () =>
                {
                    await Task.Delay(450);
                    if (_miniEnterSequence != enterSeq) return;
                    await ViewModel.ExecuteMiniAiOrPatternAsync();
                    RebuildMiniInputDocument(ViewModel.MiniInputText ?? "", focusUserInput: true);
                }, System.Windows.Threading.DispatcherPriority.ContextIdle);
            }
            else if (e.Key == Key.Up && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                ViewModel.EnterFullModeCommand.Execute(null);
                e.Handled = true;
            }
        }

        private void SearchResult_Click(object sender, MouseButtonEventArgs e) => ViewModel.ConfirmSearchResultCommand.Execute(null);

        private async Task TriggerSendProcess(InputMode mode)
        {
            this.Hide();
            await Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.Render);
            await Task.Delay(60);
            if (mode == InputMode.SmartFocus)
                await ViewModel.SendBySmartFocus();
            else
                await ViewModel.SendByCoordinate();
            if (ViewModel != null && !ViewModel.IsFullMode)
            {
                RebuildMiniInputDocument(ViewModel.MiniInputText ?? "", focusUserInput: true);
            }
        }

        private async Task TriggerSendProcess(TextBox sourceBox, InputMode mode)
        {
            sourceBox?.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
            await TriggerSendProcess(mode);
        }

        private async void TextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                var textBox = sender as TextBox;
                if (textBox == null) return;
                bool isCtrlEnter = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;
                var now = DateTime.Now;
                var span = (now - _lastVarEnterTime).TotalMilliseconds;
                if (isCtrlEnter)
                {
                    e.Handled = true;
                    await TriggerSendProcess(textBox, InputMode.SmartFocus);
                    return;
                }
                if (span < 500)
                {
                    e.Handled = true;
                    await TriggerSendProcess(textBox, InputMode.CoordinateClick);
                    _lastVarEnterTime = DateTime.MinValue;
                    return;
                }
                _lastVarEnterTime = now;
            }
        }

        private async void AdditionalInputBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                var textBox = sender as TextBox;
                if (textBox == null) return;
                bool isCtrlEnter = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;
                var now = DateTime.Now;
                var span = (now - _lastAddEnterTime).TotalMilliseconds;
                if (isCtrlEnter)
                {
                    e.Handled = true;
                    await TriggerSendProcess(textBox, InputMode.SmartFocus);
                }
                else if (span < 500)
                {
                    e.Handled = true;
                    await TriggerSendProcess(textBox, InputMode.CoordinateClick);
                    _lastAddEnterTime = DateTime.MinValue;
                }
                else
                {
                    _lastAddEnterTime = now;
                    int caretIndex = textBox.CaretIndex;
                    int lineIndex = textBox.GetLineIndexFromCharacterIndex(caretIndex);
                    if (lineIndex < 0) return;
                    string lineText = textBox.GetLineText(lineIndex);
                    var match = Regex.Match(lineText, @"^(\s*)(\d+)\.(\s+)");
                    if (match.Success)
                    {
                        string indentation = match.Groups[1].Value;
                        int currentNumber = int.Parse(match.Groups[2].Value);
                        string spacing = match.Groups[3].Value;
                        string insertText = $"\n{indentation}{currentNumber + 1}.{spacing}";
                        textBox.SelectedText = insertText;
                        textBox.CaretIndex += insertText.Length;
                        e.Handled = true;
                    }
                }
            }
        }

        private async void FileListBoxItem_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is ListBoxItem) await ViewModel.SendBySmartFocus();
        }

        private void WebDavPasswordBox_Loaded(object sender, RoutedEventArgs e) { var pb = sender as PasswordBox; if (pb != null && ViewModel.Config != null && pb.Password != ViewModel.Config.Password) pb.Password = ViewModel.Config.Password; }
        private void WebDavPasswordBox_PasswordChanged(object sender, RoutedEventArgs e) { var pb = sender as PasswordBox; if (pb != null && ViewModel.Config != null) ViewModel.Config.Password = pb.Password; }

        [System.Runtime.Versioning.SupportedOSPlatform("windows")]
        private async void PickCoordinate_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button; if (btn == null) return; string org = btn.Content.ToString() ?? "拾取";
            try
            {
                btn.IsEnabled = false;
                for (int i = 3; i > 0; i--) { btn.Content = $"{i}"; await Task.Delay(1000); }
                var pt = WinFormsCursor.Position;
                ViewModel.LocalConfig.ClickX = pt.X;
                ViewModel.LocalConfig.ClickY = pt.Y;
                btn.Content = "已获取!";
                await Task.Delay(1000);
            }
            finally { btn.Content = org; btn.IsEnabled = true; }
        }


        private async void TestAiConnection_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            if (btn == null) return;

            var statusText = this.FindName("TestStatusText") as TextBlock;
            if (statusText == null) return;

            try
            {
                statusText.Text = "🔄 测试中...";
                statusText.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(102, 102, 102));
                btn.IsEnabled = false;

                var aiService = new AiService();
                (bool success, string message) = await aiService.TestConnectionAsync(ViewModel.Config);

                if (success)
                {
                    statusText.Text = "✅ 成功连通";
                    statusText.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(67, 160, 71));

                    await Task.Delay(3000);
                    statusText.Text = "";
                }
                else
                {
                    statusText.Text = "❌ 连通失败";
                    statusText.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(229, 57, 53));
                }
            }
            catch (Exception)
            {
                statusText.Text = "❌ 连接异常";
                statusText.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(229, 57, 53));
            }
            finally
            {
                btn.IsEnabled = true;
            }
        }

        private void AiModelList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var listBox = sender as ListBox;
            if (listBox == null) return;

            if (listBox.SelectedItem is AiModelConfig selectedModel)
            {
                ViewModel.ActivateAiModelCommand.Execute(selectedModel);
            }
        }

        private void MiniModeButton_Click(object sender, RoutedEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                MiniInputBox.Focus();
                if (MiniInputBox.Document != null)
                {
                    MiniInputBox.CaretPosition = MiniInputBox.Document.ContentEnd;
                    MiniInputBox.ScrollToEnd();
                }
            }), DispatcherPriority.Input);
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                if (ViewModel != null)
                {
                    ViewModel.LocalConfig.IsMiniTopmostLocked = false;
                }
                ViewModel.ExitFullModeCommand.Execute(null);
                e.Handled = true;
            }
        }
    }
}

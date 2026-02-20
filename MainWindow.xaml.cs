using System;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using PromptMasterv5.ViewModels;
using System.Text;
using System.Windows.Media;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using PromptMasterv5.Core.Models;
using PromptMasterv5.Infrastructure.Services;
using System.Windows.Forms;
using System.Windows.Threading;
using System.Linq; // 新增引用，用于查询子窗口状态
using System.Windows.Controls.Primitives;
using System.Windows.Shapes;
using System.Windows.Documents;
using System.Text.RegularExpressions;
using System.Windows.Media.Media3D;
using Gma.System.MouseKeyHook;

// 引用自定义枚举和控件别名，解决命名冲突
using InputMode = PromptMasterv5.Core.Models.InputMode;
using Button = System.Windows.Controls.Button;
using TextBox = System.Windows.Controls.TextBox;
using ListBox = System.Windows.Controls.ListBox;
using RichTextBox = System.Windows.Controls.RichTextBox;
using WpfControl = System.Windows.Controls.Control;
using WinFormsCursor = System.Windows.Forms.Cursor;
using MessageBox = System.Windows.MessageBox;
using Application = System.Windows.Application;

using CommunityToolkit.Mvvm.Messaging;
using PromptMasterv5.ViewModels.Messages;

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
        private bool _isBackupExiting = false;
        private DispatcherTimer? _hideTimer;
        private DispatcherTimer? _miniPersistTimer;

        private const int MiniMaxAutoLines = 23;

        private double _miniDefaultHeight = 0;
        private double _miniLineHeight = 25.6;
        private double? _miniBottomAnchor;
        private DispatcherTimer? _miniAutoResizeTimer;
        private bool _isApplyingMiniAutoResize = false;
        private bool _isUpdatingMiniInputDocument = false;
        private DateTime _suppressAutoHideUntilUtc = DateTime.MinValue;

        public bool SuppressAutoActivation { get; set; }

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

        private double GetMiniDefaultHeight()
        {
            if (ViewModel == null) return 160;
            var h = ViewModel.LocalConfig.MiniDefaultHeight;
            if (h <= 0) h = ViewModel.LocalConfig.MiniWindowHeight;
            if (h <= 0) h = 118;
            if (h < 90) h = 90;
            return h;
        }

        private string GetMiniUserInputText()
        {
            if (MiniInputBox?.Document == null) return "";
            var sb = new StringBuilder();
            var hasChip = GetMiniSelectedPrompt() != null && MiniInputBox.Document.Blocks.FirstBlock is BlockUIContainer;
            var isFirst = true;

            foreach (var block in MiniInputBox.Document.Blocks)
            {
                if (isFirst && hasChip)
                {
                    isFirst = false;
                    continue;
                }
                isFirst = false;

                if (block is not Paragraph p) continue;
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
                var paragraphSpacing = MiniInputBox.FontSize * 0.8;
                doc.Resources[typeof(Paragraph)] = new Style(typeof(Paragraph))
                {
                    Setters =
                    {
                        new Setter(Paragraph.MarginProperty, new Thickness(0, 0, 0, paragraphSpacing))
                    }
                };

                var selected = GetMiniSelectedPrompt();
                if (selected != null)
                {
                    var showIcons = ViewModel.LocalConfig.MiniPinnedPromptShowIcons && !string.IsNullOrWhiteSpace(selected.IconGeometry);

                    Border chipBorder;
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
                            chipBorder = new Border
                            {
                                Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x33, 0x33, 0x33)),
                                BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x65, 0x65, 0x65)),
                                BorderThickness = new Thickness(1),
                                CornerRadius = new CornerRadius(3),
                                Padding = new Thickness(1),
                                HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
                                SnapsToDevicePixels = true
                            };
                            chipBorder.Child = path;
                        }
                        else
                        {
                            chipBorder = new Border
                            {
                                Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x33, 0x33, 0x33)),
                                BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x65, 0x65, 0x65)),
                                BorderThickness = new Thickness(1),
                                CornerRadius = new CornerRadius(3),
                                Padding = new Thickness(1),
                                HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
                                SnapsToDevicePixels = true
                            };
                            chipBorder.Child = new TextBlock
                            {
                                Text = selected.Title ?? "",
                                Foreground = TryFindResource("MiniModeBtnActiveBrush") as System.Windows.Media.Brush ?? System.Windows.Media.Brushes.White,
                                FontFamily = new System.Windows.Media.FontFamily("Segoe UI, Microsoft YaHei UI, Microsoft YaHei"),
                                FontSize = 10,
                                FontWeight = FontWeights.Normal,
                                LineHeight = double.NaN,
                                LineStackingStrategy = LineStackingStrategy.MaxHeight
                            };
                        }
                    }
                    else
                    {
                        chipBorder = new Border
                        {
                            Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x33, 0x33, 0x33)),
                            BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x65, 0x65, 0x65)),
                            BorderThickness = new Thickness(1),
                            CornerRadius = new CornerRadius(3),
                            Padding = new Thickness(1),
                            HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
                            SnapsToDevicePixels = true
                        };
                        chipBorder.Child = new TextBlock
                        {
                            Text = selected.Title ?? "",
                            Foreground = TryFindResource("MiniModeBtnActiveBrush") as System.Windows.Media.Brush ?? System.Windows.Media.Brushes.White,
                            FontFamily = new System.Windows.Media.FontFamily("Segoe UI, Microsoft YaHei UI, Microsoft YaHei"),
                            FontSize = 10,
                            FontWeight = FontWeights.Normal,
                            LineHeight = double.NaN,
                            LineStackingStrategy = LineStackingStrategy.MaxHeight
                        };
                    }

                    var chipBlock = new BlockUIContainer
                    {
                        Child = chipBorder,
                        Margin = new Thickness(0, 0, 0, 3)
                    };
                    doc.Blocks.Add(chipBlock);
                }

                Paragraph? firstUserParagraph = null;
                var normalized = (userText ?? "").Replace("\r\n", "\n").Replace("\r", "\n");
                var lines = normalized.Split('\n');
                for (int i = 0; i < lines.Length; i++)
                {
                    var line = lines[i];
                    var isLast = i == lines.Length - 1;
                    var p = new Paragraph { Margin = new Thickness(0, 0, 0, isLast ? 0 : paragraphSpacing) };
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
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (ViewModel == null) return;
                    if (ViewModel.IsFullMode) return;
                    if (MiniInputBox == null) return;
                    if (_miniDefaultHeight <= 0) return;

                    ApplyMiniAutoResize();
                }), DispatcherPriority.Background);
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

        public MainWindow(MainViewModel viewModel)
        {
            InitializeComponent();

            // 设置窗口不在任务栏显示
            this.ShowInTaskbar = false;

            ViewModel = viewModel;
            this.DataContext = ViewModel;

            ViewModel.PropertyChanged += ViewModel_PropertyChanged;
            ViewModel.ChatVM.PropertyChanged += ChatVM_PropertyChanged;
            InitializeTrayIcon();
            ApplyModeState();
            LocationChanged += MainWindow_LocationChanged;
            SizeChanged += MainWindow_SizeChanged;

            // 处理窗口关闭事件
            this.Closing += MainWindow_Closing;

            // Register message handler
            WeakReferenceMessenger.Default.Register<InsertTextToMiniInputMessage>(this, (_, m) =>
            {
                RebuildMiniInputDocument(m.Value, true);
            });
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

                // 唤醒时强制置顶（仅mini模式）
                if (ViewModel != null && !ViewModel.IsFullMode)
                    this.Topmost = true;
                NativeMethods.SetForegroundWindow(new System.Windows.Interop.WindowInteropHelper(this).Handle);
            }
        }

        private void MainWindow_Closing(object? sender, CancelEventArgs e)
        {
            // 如果正在执行备份后退出，直接跳到清理阶段
            if (_isBackupExiting)
            {
                _isExiting = true;
                // 清理托盘图标
                if (_notifyIcon != null)
                {
                    _notifyIcon.Visible = false;
                    _notifyIcon.Dispose();
                }
                return;
            }

            // 检查是否有未保存的修改
            if (ViewModel.IsDirty)
            {
                // 检查是否配置了WebDAV同步
                bool hasWebDav = !string.IsNullOrWhiteSpace(ViewModel.Config.WebDavUrl) 
                              && !string.IsNullOrWhiteSpace(ViewModel.Config.Password);
                
                if (hasWebDav)
                {
                    // 配置了WebDAV，询问是否备份到云端
                    var dialog = new Views.ConfirmationDialog
                    {
                        Owner = this
                    };
                    dialog.ShowDialog();
                    var result = dialog.Result;
                    
                    if (result == MessageBoxResult.Cancel)
                    {
                        e.Cancel = true; // 取消关闭操作
                        return;
                    }
                    else if (result == MessageBoxResult.Yes)
                    {
                        e.Cancel = true; // 取消本次关闭，等待备份完成后再关闭
                        _ = BackupAndExitAsync();
                        return;
                    }
                    // 如果选 No，继续执行（App.OnExit 会自动保存到本地）
                }
                else
                {
                    // 未配置WebDAV，静默保存到本地（通过 App.OnExit）
                    // 不显示任何提示，因为本地保存是自动的
                }
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

        /// <summary>
        /// 执行云端备份，完成后自动退出应用
        /// </summary>
        private async Task BackupAndExitAsync()
        {
            try
            {
                Title = "正在备份到云端... 请勿关闭";
                await ViewModel.BackupToCloudAsync();
            }
            catch (Exception ex)
            {
                Infrastructure.Services.LoggerService.Instance.LogException(ex, "Exit backup failed", "MainWindow.BackupAndExitAsync");
            }
            finally
            {
                // 设置标志位，下次 Close() 时直接跳过对话框
                _isBackupExiting = true;
                _isExiting = true;
                this.Close();
            }
        }

        private void Window_Activated(object sender, EventArgs e)
        {
            if (SuppressAutoActivation) return;

            // 1. 窗口被激活（唤醒）时，取消任何待执行的隐藏操作
            StopHideTimer();

            // 2. 需求实现：唤醒时置顶显示（仅mini模式）
            if (ViewModel != null && !ViewModel.IsFullMode)
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
            if (DateTime.UtcNow < _suppressAutoHideUntilUtc)
            {
                return;
            }
            if (ViewModel != null && ViewModel.LocalConfig.IsMiniTopmostLocked)
            {
                return;
            }

            // 1. 需求实现：不需要始终保持置顶
            // 当失去焦点（用户点击了其他软件）时，取消置顶，允许其他窗口覆盖它
            this.Topmost = false;

            // 取消之前的定时器
            StopHideTimer();

            // 如果是完整模式，不执行自动隐藏
            if (ViewModel != null && ViewModel.IsFullMode)
            {
                return;
            }

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

        public void SuppressMiniAutoHide(int milliseconds)
        {
            _suppressAutoHideUntilUtc = DateTime.UtcNow.AddMilliseconds(milliseconds);
            StopHideTimer();
        }

        public void BringToFrontAndEnsureOnScreen()
        {
            SuppressMiniAutoHide(800);
            EnsureWindowOnScreen();
            if (ViewModel == null || !ViewModel.IsFullMode)
                Topmost = true;
            Show();
            Activate();
            Focus();
            NativeMethods.SetForegroundWindow(new System.Windows.Interop.WindowInteropHelper(this).Handle);
        }

        private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MainViewModel.IsFullMode))
            {
                ApplyModeState();
            }
        }

        private void ChatVM_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (ViewModel == null) return;
            if (e.PropertyName != nameof(ChatViewModel.MiniInputText)) return;

            if (string.IsNullOrEmpty(ViewModel.ChatVM.MiniInputText) && !ViewModel.IsFullMode)
            {
                _ = Dispatcher.BeginInvoke(new Action(() =>
                {
                    ResetMiniWindowToDefaultSize();
                }), System.Windows.Threading.DispatcherPriority.ContextIdle);
            }
            else if (!ViewModel.IsFullMode && !_isUpdatingMiniInputDocument && MiniInputBox != null && !MiniInputBox.IsKeyboardFocusWithin)
            {
                RebuildMiniInputDocument(ViewModel.ChatVM.MiniInputText ?? "", focusUserInput: false);
            }
        }

        private void ResetMiniWindowToDefaultSize()
        {
            if (ViewModel.IsFullMode) return;
            if (_miniDefaultHeight <= 0)
            {
                // 如果 _miniDefaultHeight 尚未初始化，尝试初始化
                if (ViewModel.LocalConfig.MiniUseDefaultSize)
                {
                    _miniDefaultHeight = GetMiniDefaultHeight();
                }
                else
                {
                    // 记忆模式下，_miniDefaultHeight 应该是记忆的高度
                    var h = ViewModel.LocalConfig.MiniWindowHeight;
                    if (h > 0) _miniDefaultHeight = h;
                    else _miniDefaultHeight = GetMiniDefaultHeight();
                }
            }
            
            if (_miniDefaultHeight <= 0)
            {
                EnsureMiniDefaultSizeMeasured();
                return;
            }

            var bottom = _miniBottomAnchor ?? (Top + Height);
            var targetHeight = _miniDefaultHeight;
            
            // 确定目标宽度
            var targetWidth = 0.0;
            if (ViewModel.LocalConfig.MiniUseDefaultSize)
            {
                targetWidth = GetMiniDefaultWidth();
            }
            else
            {
                targetWidth = ViewModel.LocalConfig.MiniWindowWidth;
                if (targetWidth <= 0) targetWidth = GetMiniDefaultWidth();
            }

            _isApplyingMiniAutoResize = true;
            Width = targetWidth;
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

                // 完整模式不置顶
                this.Topmost = false;
                EnsureWindowOnScreen();
            }
            else
            {
                _lastFullWidth = this.Width;
                _lastFullHeight = this.Height;
                _lastFullLeft = this.Left;
                _lastFullTop = this.Top;

                var cfg = ViewModel.LocalConfig;

                // 1. 应用尺寸逻辑
                if (cfg.MiniUseDefaultSize)
                {
                    this.Width = GetMiniDefaultWidth();
                    this.Height = GetMiniDefaultHeight();
                }
                else
                {
                    // 使用记忆尺寸，如果无效则回退到默认
                    var w = cfg.MiniWindowWidth;
                    var h = cfg.MiniWindowHeight;
                    if (w <= 0) w = GetMiniDefaultWidth();
                    if (h <= 0) h = GetMiniDefaultHeight();
                    this.Width = w;
                    this.Height = h;
                }
                _miniDefaultHeight = this.Height;

                // 2. 应用位置逻辑
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

                if (this.Height < 90) this.Height = 90;
                
                // 如果是指定位置模式，根据 BottomAnchor 计算 Top
                if (cfg.MiniUseDefaultPosition && _miniBottomAnchor.HasValue) 
                {
                    this.Top = _miniBottomAnchor.Value - this.Height;
                }
                
                _miniBottomAnchor ??= Top + Height;

                this.ResizeMode = ResizeMode.NoResize;

                // 切换模式时，确保重置Topmost并激活
                this.Topmost = true;
                EnsureWindowOnScreen();
                this.Activate();
                NativeMethods.SetForegroundWindow(new System.Windows.Interop.WindowInteropHelper(this).Handle);

                EnsureMiniDefaultSizeMeasured();
                RebuildMiniInputDocument(ViewModel.ChatVM.MiniInputText ?? "", focusUserInput: true);
                ApplyMiniScrollBarAppearance(isOverflow: false);
            }
        }

        private void EnsureMiniDefaultSizeMeasured()
        {
            if (ViewModel == null) return;
            if (ViewModel.IsFullMode) return;

            var cfg = ViewModel.LocalConfig;
            
            if (cfg.MiniUseDefaultSize)
            {
                if (cfg.MiniDefaultHeight > 0)
                {
                    _miniDefaultHeight = GetMiniDefaultHeight();
                    return;
                }
            }
            else
            {
                // 记忆模式
                if (cfg.MiniWindowHeight > 0)
                {
                    _miniDefaultHeight = cfg.MiniWindowHeight;
                    return;
                }
            }

            _ = Dispatcher.BeginInvoke(new Action(() =>
            {
                if (ViewModel == null) return;
                if (ViewModel.IsFullMode) return;
                if (MiniInputBox == null) return;

                var lineHeightObj = MiniInputBox.GetValue(TextBlock.LineHeightProperty);
                _miniLineHeight = lineHeightObj is double lh && lh > 0 ? lh : 25.6;

                var modeBarHeight = MiniModeBar?.ActualHeight ?? 0;
                if (modeBarHeight < 12) modeBarHeight = 12;

                var overhead = ActualHeight - MiniInputBox.ActualHeight - modeBarHeight;
                if (overhead < 40) overhead = 40;

                _miniDefaultHeight = overhead + modeBarHeight + _miniLineHeight;
                if (_miniDefaultHeight < 90) _miniDefaultHeight = 90;
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
                ViewModel.ChatVM.MiniInputText = GetMiniUserInputText();
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

        private void MiniInput_PreviewTextInputStart(object sender, TextCompositionEventArgs e)
        {
            if (ViewModel == null) return;
            if (ViewModel.IsFullMode) return;
            if (MiniInputBox == null) return;
            if (!ViewModel.ChatVM.IsAiResultDisplayed) return;
            if (!ViewModel.LocalConfig.MiniClearAiResultOnTyping) return;

            ViewModel.ChatVM.IsAiResultDisplayed = false;
            ViewModel.ChatVM.MiniInputText = "";
            RebuildMiniInputDocument("", focusUserInput: true);
        }

        private void MiniInput_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            if (ViewModel == null) return;
            if (ViewModel.IsFullMode) return;
            if (MiniInputBox == null) return;
            if (!ViewModel.ChatVM.IsAiResultDisplayed) return;
            if (!ViewModel.LocalConfig.MiniClearAiResultOnTyping) return;
            if (string.IsNullOrEmpty(e.Text)) return;

            var textToInsert = e.Text;
            e.Handled = true;

            ViewModel.ChatVM.IsAiResultDisplayed = false;
            ViewModel.ChatVM.MiniInputText = "";
            RebuildMiniInputDocument("", focusUserInput: true);

            var caret = MiniInputBox.CaretPosition?.GetInsertionPosition(LogicalDirection.Forward) ?? MiniInputBox.CaretPosition;
            if (caret == null) return;

            caret.InsertTextInRun(textToInsert);
            MiniInputBox.CaretPosition = caret.GetPositionAtOffset(textToInsert.Length, LogicalDirection.Forward) ?? MiniInputBox.CaretPosition;
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
            var wa = SystemParameters.WorkArea;

            if (Width <= 0 || double.IsNaN(Width)) Width = Math.Min(1000, wa.Width);
            if (Height <= 0 || double.IsNaN(Height)) Height = Math.Min(600, wa.Height);

            var maxLeft = wa.Right - Width;
            var maxTop = wa.Bottom - Height;

            if (maxLeft < wa.Left) maxLeft = wa.Left;
            if (maxTop < wa.Top) maxTop = wa.Top;

            if (Left < wa.Left) Left = wa.Left;
            if (Top < wa.Top) Top = wa.Top;
            if (Left > maxLeft) Left = maxLeft;
            if (Top > maxTop) Top = maxTop;
        }



        private void MiniPinnedPromptItem_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel == null) return;
            if (ViewModel.IsFullMode) return;

            if (sender is not Button btn) return;
            var id = btn.Tag as string ?? "";
            if (string.IsNullOrWhiteSpace(id)) return;

            var userText = ViewModel.ChatVM.MiniInputText ?? "";
            if (ViewModel.LocalConfig.MiniSelectedPinnedPromptId == id)
            {
                ViewModel.LocalConfig.MiniSelectedPinnedPromptId = "";
            }
            else
            {
                if (ViewModel.LocalConfig.MiniPinnedPromptClickShowsFullContent)
                {
                    // Full Content Mode: Show Text ONLY (No Chip)
                    ViewModel.LocalConfig.MiniSelectedPinnedPromptId = ""; 
                    var p = ViewModel.Files.FirstOrDefault(f => f.Id == id);
                    userText = p?.Content ?? "";
                    ViewModel.ChatVM.MiniInputText = userText;
                }
                else
                {
                    // Combo Mode: Show Chip + Preserve Text
                    ViewModel.LocalConfig.MiniSelectedPinnedPromptId = id;
                }
            }

            RebuildMiniInputDocument(userText, focusUserInput: true);
        }


        private async void MiniInput_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            var box = sender as RichTextBox;
            if (box == null) return;
            if (ViewModel == null) return;

            if (!ViewModel.IsFullMode &&
                ViewModel.ChatVM.IsAiResultDisplayed &&
                ViewModel.LocalConfig.MiniClearAiResultOnTyping &&
                e.Key == Key.ImeProcessed)
            {
                ViewModel.ChatVM.IsAiResultDisplayed = false;
                ViewModel.ChatVM.MiniInputText = "";
                RebuildMiniInputDocument("", focusUserInput: true);
                return;
            }

            if (!ViewModel.IsFullMode && !_isUpdatingMiniInputDocument)
            {
                ViewModel.ChatVM.MiniInputText = GetMiniUserInputText();
            }

            if (e.Key == Key.Enter && (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
            {
                var paragraph = box.CaretPosition?.Paragraph;
                if (paragraph != null)
                {
                    var selected = GetMiniSelectedPrompt();
                    var chipParagraph = selected != null ? box.Document?.Blocks.FirstBlock as Paragraph : null;
                    var inChip = chipParagraph != null && ReferenceEquals(paragraph, chipParagraph);

                    if (!inChip)
                    {
                        var full = new TextRange(paragraph.ContentStart, paragraph.ContentEnd).Text ?? "";
                        full = full.TrimEnd('\r', '\n');
                        var m = Regex.Match(full, @"^\s*(\d+)\.\s");
                        if (m.Success && int.TryParse(m.Groups[1].Value, out var n))
                        {
                            e.Handled = true;
                            EditingCommands.EnterParagraphBreak.Execute(null, box);
                            var prefix = $"{n + 1}. ";
                            var newParagraph = box.CaretPosition?.Paragraph;
                            if (newParagraph != null)
                            {
                                var run = new Run(prefix);
                                var firstInline = newParagraph.Inlines.FirstInline;
                                if (firstInline != null)
                                {
                                    newParagraph.Inlines.InsertBefore(firstInline, run);
                                }
                                else
                                {
                                    newParagraph.Inlines.Add(run);
                                }
                                box.CaretPosition = run.ContentEnd;
                            }
                            return;
                        }
                    }
                }
                return;
            }

            if ((e.Key == Key.Delete || e.Key == Key.Back) && !ViewModel.IsFullMode)
            {
                var selectedId = ViewModel.LocalConfig.MiniSelectedPinnedPromptId ?? "";
                if (!string.IsNullOrWhiteSpace(selectedId) && box.Document != null)
                {
                    var userText = ViewModel.ChatVM.MiniInputText ?? GetMiniUserInputText();

                    var caret = box.CaretPosition.GetInsertionPosition(LogicalDirection.Forward);
                    var docStart = box.Document.ContentStart.GetInsertionPosition(LogicalDirection.Forward);

                    var hasChip = box.Document.Blocks.FirstBlock is BlockUIContainer;
                    var firstUserPara = box.Document.Blocks.OfType<Paragraph>().FirstOrDefault();

                    var atDocStart = caret != null && docStart != null && caret.CompareTo(docStart) == 0;
                    var atUserStart = firstUserPara != null && caret != null && caret.CompareTo(firstUserPara.ContentStart) == 0;

                    var shouldRemoveChip =
                        hasChip &&
                        ((e.Key == Key.Delete && atDocStart) ||
                         (e.Key == Key.Back && atUserStart));

                    if (shouldRemoveChip)
                    {
                        ViewModel.LocalConfig.MiniSelectedPinnedPromptId = "";
                        RebuildMiniInputDocument(userText, focusUserInput: true);
                        e.Handled = true;
                        return;
                    }
                }
            }

            if (e.Key == Key.Delete && (ViewModel.LocalConfig.MiniAiOnlyChatEnabled || ViewModel.ChatVM.IsAiResultDisplayed))
            {
                ViewModel.ChatVM.MiniInputText = "";
                ViewModel.ChatVM.IsAiResultDisplayed = false;
                RebuildMiniInputDocument("", focusUserInput: true);
                e.Handled = true;
                return;
            }

            if (ViewModel.ChatVM.IsSearchPopupOpen && ViewModel.ChatVM.SearchResults.Count > 0)
            {
                if (e.Key == Key.Down)
                {
                    int newIndex = SearchListBox.SelectedIndex + 1;
                    if (newIndex >= ViewModel.ChatVM.SearchResults.Count) newIndex = 0;
                    SearchListBox.SelectedIndex = newIndex;
                    SearchListBox.ScrollIntoView(SearchListBox.SelectedItem);
                    e.Handled = true;
                    return;
                }
                else if (e.Key == Key.Up)
                {
                    int newIndex = SearchListBox.SelectedIndex - 1;
                    if (newIndex < 0) newIndex = ViewModel.ChatVM.SearchResults.Count - 1;
                    SearchListBox.SelectedIndex = newIndex;
                    SearchListBox.ScrollIntoView(SearchListBox.SelectedItem);
                    e.Handled = true;
                    return;
                }
            }

            if (e.Key == Key.Enter)
            {
                if (ViewModel.ChatVM.IsSearchPopupOpen)
                {
                        ViewModel.ChatVM.ConfirmSearchResultCommand.Execute(null);
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
                            RebuildMiniInputDocument(ViewModel.ChatVM.MiniInputText ?? "", focusUserInput: true);
                    }), System.Windows.Threading.DispatcherPriority.ContextIdle);
                    return;
                }

                // Auto-Numbering Logic for Enter key (No modifiers)
                if (Keyboard.Modifiers == ModifierKeys.None)
                {
                    var paragraph = box.CaretPosition?.Paragraph;
                    if (paragraph != null)
                    {
                        var selected = GetMiniSelectedPrompt();
                        var chipParagraph = selected != null ? box.Document?.Blocks.FirstBlock as Paragraph : null;
                        var inChip = chipParagraph != null && ReferenceEquals(paragraph, chipParagraph);

                        if (!inChip)
                        {
                            var full = new TextRange(paragraph.ContentStart, paragraph.ContentEnd).Text ?? "";
                            full = full.TrimEnd('\r', '\n');

                            // Case 1: Empty List Item (e.g. "2. ") -> Exit List
                            var emptyMatch = Regex.Match(full, @"^\s*\d+\.\s*$");
                            if (emptyMatch.Success)
                            {
                                e.Handled = true;
                                paragraph.Inlines.Clear(); // Remove the number
                                return; // Handled, no send. Next Enter will be empty line -> Send (if logic permits) or just newline
                            }

                            // Case 2: Content List Item (e.g. "1. Text") -> Continue List
                            var match = Regex.Match(full, @"^\s*(\d+)\.\s");
                            if (match.Success && int.TryParse(match.Groups[1].Value, out var n))
                            {
                                e.Handled = true;
                                EditingCommands.EnterParagraphBreak.Execute(null, box);
                                var prefix = $"{n + 1}. ";
                                var newParagraph = box.CaretPosition?.Paragraph;
                                if (newParagraph != null)
                                {
                                    var run = new Run(prefix);
                                    var firstInline = newParagraph.Inlines.FirstInline;
                                    if (firstInline != null)
                                    {
                                        newParagraph.Inlines.InsertBefore(firstInline, run);
                                    }
                                    else
                                    {
                                        newParagraph.Inlines.Add(run);
                                    }
                                    box.CaretPosition = run.ContentEnd;
                                }
                                return;
                            }
                        }
                    }
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
                        await ViewModel.ChatVM.ExecuteMiniAiOrPatternAsync();
                        RebuildMiniInputDocument(ViewModel.ChatVM.MiniInputText ?? "", focusUserInput: true);
                }, System.Windows.Threading.DispatcherPriority.ContextIdle);
            }
            else if (e.Key == Key.Up && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                ViewModel.EnterFullModeCommand.Execute(null);
                e.Handled = true;
            }
        }

        private void SearchResult_Click(object sender, MouseButtonEventArgs e) => ViewModel.ChatVM.ConfirmSearchResultCommand.Execute(null);

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
                RebuildMiniInputDocument(ViewModel.ChatVM.MiniInputText ?? "", focusUserInput: true);
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
                    // Execute Web Target Send
                    ViewModel.SendDefaultWebTargetCommand.Execute(null);
                    return;
                }
                
                if (span < 500 && ViewModel.Config.EnableDoubleEnterSend)
                {
                    e.Handled = true;
                    // Execute Web Target Send
                    ViewModel.SendDefaultWebTargetCommand.Execute(null);
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
                    ViewModel.SendDefaultWebTargetCommand.Execute(null);
                }
                else if (span < 500 && ViewModel.Config.EnableDoubleEnterSend)
                {
                    e.Handled = true;
                    ViewModel.SendDefaultWebTargetCommand.Execute(null);
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
                    ViewModel.ExitFullModeCommand.Execute(null);
                }
                e.Handled = true;
            }
        }
        private void Block3ContentEditor_LostFocus(object sender, RoutedEventArgs e)
        {
            CheckAndExitEditMode();
        }

        private void Block3TitleEditor_LostFocus(object sender, RoutedEventArgs e)
        {
            CheckAndExitEditMode();
        }

        private void CheckAndExitEditMode()
        {
            if (ViewModel == null || !ViewModel.IsEditMode) return;

            // 检查新的焦点是否仍在编辑区内
            var focused = Keyboard.FocusedElement as DependencyObject;
            if (IsBlock3Editor(focused)) return;

            ViewModel.IsEditMode = false;
            ViewModel.RequestSaveCommand.Execute(null);
        }

        private bool IsBlock3Editor(DependencyObject? obj)
        {
            if (obj == null) return false;
            if (obj == Block3TitleEditor) return true;
            if (obj == Block3ContentEditor) return true;
            
            // 检查是否是编辑器的子元素（例如右键菜单或内部结构）
            if (Block3TitleEditor != null && Block3TitleEditor.IsAncestorOf(obj)) return true;
            if (Block3ContentEditor != null && Block3ContentEditor.IsAncestorOf(obj)) return true;
            
            return false;
        }
    }
}

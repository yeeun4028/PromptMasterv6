using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using GongSolutions.Wpf.DragDrop;
using Markdig;
using Microsoft.Extensions.DependencyInjection;
using PromptMasterv6.Core.Interfaces;
using PromptMasterv6.Core.Models;
using PromptMasterv6.Infrastructure.Helpers;
using PromptMasterv6.Infrastructure.Services;
using PromptMasterv6.ViewModels.Messages;
using ReverseMarkdown;


using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Threading;
using Application = System.Windows.Application;
using Clipboard = System.Windows.Clipboard;
using IDropTarget = GongSolutions.Wpf.DragDrop.IDropTarget;
using MessageBox = System.Windows.MessageBox;

using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace PromptMasterv6.ViewModels;

public partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly IDataService _dataService;
    private readonly IDataService _localDataService;
    private readonly GlobalKeyService _keyService;
    private readonly IAiService _aiService;
    private readonly IDialogService _dialogService;
    private readonly ClipboardService _clipboardService;
    private readonly IWindowManager _windowManager; // Injected
    private readonly ISettingsService _settingsService;
    private readonly HotkeyService _hotkeyService;

    // 编译后的正则表达式，用于解析变量 {{xxx}}
    private static readonly Regex VariableRegex = new(@"\{\{(.*?)\}\}", RegexOptions.Compiled);
    
    // 编译后的正则表达式，用于检测 HTML 标签（带超时保护）
    private static readonly Regex HtmlTagRegex = new(
        @"<(p|div|br|span|a|img|ul|ol|li|table|tr|td|th|h[1-6]|strong|em|b|i|u|code|pre|blockquote|script|style|iframe|form|input|button)[\s>/]",
        RegexOptions.IgnoreCase | RegexOptions.Compiled,
        TimeSpan.FromMilliseconds(100)
    );

    private DispatcherTimer _timer;
    private readonly Subject<System.Reactive.Unit> _saveSubject = new();
    private readonly Subject<System.Reactive.Unit> _saveLocalSettingsSubject = new();

    private EventHandler? _onLauncherTriggeredHandler;
    private PropertyChangedEventHandler? _localConfigPropertyChangedHandler;

    private bool _isSimulatingKeys;
    public void SetSimulatingKeys(bool value) => _isSimulatingKeys = value;

    [ObservableProperty] private AppConfig config;
    [ObservableProperty] private LocalSettings localConfig;
    [ObservableProperty] private bool isFullMode = true;

    public ISettingsService SettingsService => _settingsService;

    [ObservableProperty] private string syncTimeDisplay = "Now";
    [ObservableProperty] private ICollectionView? filesView;

    [ObservableProperty] private ObservableCollection<FolderItem> folders = new();
    [ObservableProperty] private FolderItem? selectedFolder;

    [ObservableProperty] private ObservableCollection<PromptItem> files = new();

    [ObservableProperty] private PromptItem? selectedFile;

    partial void OnSelectedFileChanged(PromptItem? value)
    {
        IsEditMode = false;
        PreviewContent = SafeConvertHtmlToMarkdown(value?.Content);
        SafeParseVariables(value?.Content ?? "");
    }

    [RelayCommand]
    private void RenameFile(PromptItem? item)
    {
        if (item != null)
        {
            item.IsRenaming = true;
        }
    }

    private string? SafeConvertHtmlToMarkdown(string? content)
    {
        try
        {
            return ConvertHtmlToMarkdown(content);
        }
        catch (Exception ex)
        {
            Infrastructure.Services.LoggerService.Instance.LogException(ex, "HTML转Markdown失败", "SafeConvertHtmlToMarkdown");
            return content ?? "";
        }
    }

    private void SafeParseVariables(string content)
    {
        try
        {
            ParseVariablesRealTime(content);
        }
        catch (Exception ex)
        {
            Infrastructure.Services.LoggerService.Instance.LogException(ex, "变量解析失败", "SafeParseVariables");
        }
    }

    private string? ConvertHtmlToMarkdown(string? content)
    {
        if (string.IsNullOrWhiteSpace(content)) return content;
        
        if (ContainsHtml(content))
        {
            try
            {
                var config = new ReverseMarkdown.Config
                {
                    UnknownTags = ReverseMarkdown.Config.UnknownTagsOption.PassThrough,
                    GithubFlavored = true,
                    RemoveComments = false
                };

                var converter = new ReverseMarkdown.Converter(config);
                return converter.Convert(content);
            }
            catch (Exception ex)
            {
                Infrastructure.Services.LoggerService.Instance.LogException(ex, "HTML转Markdown失败", "ConvertHtmlToMarkdown");
                return content;
            }
        }
        
        return content;
    }
    
    private bool ContainsHtml(string content)
    {
        if (string.IsNullOrEmpty(content)) return false;
        try
        {
            return HtmlTagRegex.IsMatch(content);
        }
        catch (RegexMatchTimeoutException)
        {
            return false;
        }
    }

    [ObservableProperty] private bool isEditMode;
    [ObservableProperty] private ObservableCollection<VariableItem> variables = new();
    [ObservableProperty] private bool hasVariables;
    [ObservableProperty] private string additionalInput = "";

    [ObservableProperty] private bool isDirty;

    // 记录进入编辑模式时的原始内容，用于判断内容是否真正发生变化
    private string? _originalContentBeforeEdit;

    public MarkdownPipeline Pipeline { get; }

    [ObservableProperty] private string? previewContent;


    public MainViewModel(
        ISettingsService settingsService,
        IAiService aiService,
        WebDavDataService dataService,
        FileDataService localDataService,
        GlobalKeyService keyService,
        IDialogService dialogService,
        ClipboardService clipboardService,
        IWindowManager windowManager)
    {
        Pipeline = new MarkdownPipelineBuilder()
            .UseSoftlineBreakAsHardlineBreak()
            .DisableHtml()
            .Build();

        _aiService = aiService;
        _dataService = dataService;
        _localDataService = localDataService;
        _keyService = keyService;
        _dialogService = dialogService;
        _clipboardService = clipboardService;
        _settingsService = settingsService;
        _windowManager = windowManager;
        _hotkeyService = new HotkeyService();

        Config = settingsService.Config;
        LocalConfig = settingsService.LocalConfig;

        WeakReferenceMessenger.Default.Register<FolderSelectionChangedMessage>(this, (_, __) =>
        {
            UpdateFilesViewFilter();
            FilesView?.Refresh();
            
            if (FilesView != null && !FilesView.IsEmpty)
            {
                var firstItem = FilesView.Cast<PromptItem>().FirstOrDefault();
                SelectedFile = firstItem;
            }
            else
            {
                SelectedFile = null;
            }
        });
        WeakReferenceMessenger.Default.Register<RequestSelectFileMessage>(this, (_, m) =>
        {
            SelectedFile = m.File;
            if (m.EnterEditMode) IsEditMode = true;
        });
        WeakReferenceMessenger.Default.Register<RequestMoveFileToFolderMessage>(this, (_, m) => MoveFileToFolder(m.File, m.TargetFolder));
        WeakReferenceMessenger.Default.Register<RequestSaveMessage>(this, (_, __) => RequestSave());
        WeakReferenceMessenger.Default.Register<RequestBackupMessage>(this, async (_, __) => await PerformLocalBackup());
        WeakReferenceMessenger.Default.Register<ToggleWindowMessage>(this, (_, __) => ToggleMainWindow());
        WeakReferenceMessenger.Default.Register<TriggerLauncherMessage>(this, (_, __) => HandleLauncherTriggered());
        WeakReferenceMessenger.Default.Register<JumpToEditPromptMessage>(this, (_, m) =>
        {
            if (m.File != null)
            {
                SelectedFile = m.File;
                IsEditMode = true;
            }
        });
        WeakReferenceMessenger.Default.Register<RequestPromptFileMessage>(this, (r, m) =>
        {
            var file = Files.FirstOrDefault(f => f.Id == m.PromptId);
            m.Reply(new PromptFileResponseMessage { File = file });
        });


        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, __) => UpdateTimeDisplay();
        _timer.Start();

        _saveSubject
            .Throttle(TimeSpan.FromSeconds(5))
            .ObserveOn(System.Threading.SynchronizationContext.Current!)
            .Subscribe(async _ => await PerformLocalBackup());

        _saveLocalSettingsSubject
            .Throttle(TimeSpan.FromSeconds(2))
            .ObserveOn(System.Threading.SynchronizationContext.Current!)
            .Subscribe(_ => _settingsService.SaveLocalConfig());

        _localConfigPropertyChangedHandler = (_, e) =>
        {
            if (e.PropertyName == nameof(LocalSettings.Block1Width) ||
                e.PropertyName == nameof(LocalSettings.Block2Width))
            {
                _saveLocalSettingsSubject.OnNext(System.Reactive.Unit.Default);
            }
        };
        LocalConfig.PropertyChanged += _localConfigPropertyChangedHandler;

        _onLauncherTriggeredHandler = (_, __) => HandleLauncherTriggered();
        _keyService.OnLauncherTriggered += _onLauncherTriggeredHandler;

        _keyService.LauncherHotkeyString = Config.LauncherHotkey;
        try { _keyService.Start(); }
        catch (Exception ex)
        {
            LoggerService.Instance.LogException(ex, "Failed to start GlobalKeyService", "MainViewModel.ctor");
        }

        UpdateWindowHotkeys();

        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        AppData data;
        try
        {
            data = await _dataService.LoadAsync();
        }
        catch
        {
            data = new AppData();
        }

        if ((data.Folders?.Count ?? 0) == 0 && (data.Files?.Count ?? 0) == 0)
        {
            try
            {
                data = await _localDataService.LoadAsync();
            }
            catch
            {
                data = new AppData();
            }
        }

        // 不再从 WebDAV 同步 API 配置和 AI 模型配置
        // 这些配置仅通过本地 config.json 保存，避免覆盖用户的本地设置
        // if (data.ApiProfiles != null && data.ApiProfiles.Any())
        // {
        //     Config.ApiProfiles = new ObservableCollection<ApiProfile>(data.ApiProfiles);
        //     configUpdated = true;
        // }
        // if (data.SavedModels != null && data.SavedModels.Any())
        // {
        //     Config.SavedModels = new ObservableCollection<AiModelConfig>(data.SavedModels);
        //     configUpdated = true;
        // }
        // if (configUpdated)
        // {
        //     Infrastructure.Services.ConfigService.Save(Config);
        // }

        Files = new ObservableCollection<PromptItem>(data.Files ?? new());

        Files.CollectionChanged += OnFilesCollectionChanged;
        foreach (var item in Files)
        {
            item.PropertyChanged += OnFilePropertyChanged;
        }

        Folders = new ObservableCollection<FolderItem>(data.Folders ?? new());
        if (Folders.Count == 0)
        {
            var defaultFolder = new FolderItem { Name = "默认" };
            Folders.Add(defaultFolder);
            SelectedFolder = defaultFolder;
        }
        else
        {
            SelectedFolder = Folders.FirstOrDefault();
        }

        if (SelectedFolder != null)
        {
            foreach (var f in Files)
            {
                if (string.IsNullOrWhiteSpace(f.FolderId))
                {
                    f.FolderId = SelectedFolder.Id;
                }
            }
        }

        FilesView = CollectionViewSource.GetDefaultView(Files);
        UpdateFilesViewFilter();
        FilesView?.Refresh();

        if (FilesView != null && !FilesView.IsEmpty)
        {
            var firstItem = FilesView.Cast<PromptItem>().FirstOrDefault();
            if (firstItem != null)
            {
                SelectedFile = firstItem;
            }
        }

        IsDirty = false;
    }

    public void UpdateFilesViewFilter()
    {
        if (FilesView == null) return;

        var selectedFolderId = SelectedFolder?.Id;
        FilesView.Filter = item =>
        {
            if (item is not PromptItem f) return false;
            if (string.IsNullOrWhiteSpace(selectedFolderId)) return true;
            return string.Equals(f.FolderId, selectedFolderId, StringComparison.Ordinal);
        };
    }

    [RelayCommand]
    private void EnterFullMode()
    {
        IsFullMode = true;
    }

    public void OnWindowHotkeyPressed()
    {
        ToggleMainWindow();
    }

    public void SimulateFullWindowHotkey()
    {
        _hotkeyService.SimulateHotkey(Config.FullWindowHotkey);
    }

    public void ToggleModeViaHotkey()
    {
        EnterFullMode();
    }

    [RelayCommand]
    private void OpenSettings()
    {
        _windowManager.ShowSettingsWindow();
    }

    [RelayCommand]
    private void ImportMarkdownFiles()
    {
        string filter = "Markdown 文件 (*.md;*.markdown)|*.md;*.markdown|所有文件 (*.*)|*.*";
        var files = _dialogService.ShowOpenFilesDialog(filter);

        if (files == null || files.Length == 0) return;

        var targetFolder = SelectedFolder;
        if (targetFolder == null)
        {
            targetFolder = new FolderItem { Name = "导入" };
            Folders.Add(targetFolder);
            SelectedFolder = targetFolder;
        }

        foreach (var filePath in files)
        {
            try
            {
                var content = File.ReadAllText(filePath);
                var title = Path.GetFileNameWithoutExtension(filePath);
                Files.Add(new PromptItem
                {
                    Title = title,
                    Content = content,
                    FolderId = targetFolder.Id,
                    LastModified = DateTime.Now
                });
            }
            catch
            {
            }
        }

        UpdateFilesViewFilter();
        FilesView?.Refresh();
        RequestSave();
    }

    [RelayCommand]
    private void DeleteFile(PromptItem? file)
    {
        if (file == null) return;
        Files.Remove(file);
        if (SelectedFile == file) SelectedFile = null;
        RequestSave();
    }

    [RelayCommand]
    private void ChangeFileIcon(PromptItem? file)
    {
        if (file == null) return;
        var dialog = new PromptMasterv6.IconInputDialog(file.IconGeometry);
        if (dialog.ShowDialog() == true)
        {
            file.IconGeometry = dialog.ResultGeometry;
            RequestSave();
        }
    }

    [RelayCommand]
    private void ToggleEditMode()
    {
        if (SelectedFile == null)
        {
            IsEditMode = false;
            return;
        }

        if (IsEditMode)
        {
            // 检查内容是否真正发生了变化
            bool contentChanged = !string.Equals(_originalContentBeforeEdit, SelectedFile.Content, StringComparison.Ordinal);

            if (contentChanged)
            {
                SelectedFile.LastModified = DateTime.Now;
                RequestSave();
            }

            IsEditMode = false;
            PreviewContent = ConvertHtmlToMarkdown(SelectedFile.Content);
            _originalContentBeforeEdit = null; // 清除记录
            return;
        }

        // 进入编辑模式时，记录当前内容
        _originalContentBeforeEdit = SelectedFile.Content;
        IsEditMode = true;
    }

    [RelayCommand]
    private void CopyCompiledText()
    {
        var text = CompileContent();
        if (string.IsNullOrWhiteSpace(text)) return;
        _clipboardService.SetClipboard(text);
    }

    [RelayCommand]
    private async Task SendDefaultWebTarget()
    {
        if (SelectedFile == null) return;
        var targetName = Config.DefaultWebTargetName;
        var target = Config.WebDirectTargets.FirstOrDefault(t => t.Name == targetName);

        if (target != null)
        {
            if (!target.IsEnabled)
            {
                _dialogService.ShowAlert($"默认目标 '{targetName}' 已被禁用，请在设置中启用。", "目标不可用");
                return;
            }
            await OpenWebTarget(target).ConfigureAwait(false);
        }
        else
        {
            // Fallback: try finding Gemini or first available
            target = Config.WebDirectTargets.FirstOrDefault(t => t.Name == "Gemini" && t.IsEnabled)
                     ?? Config.WebDirectTargets.FirstOrDefault(t => t.IsEnabled);

            if (target != null)
            {
                await OpenWebTarget(target).ConfigureAwait(false);
            }
            else
            {
                _dialogService.ShowAlert($"未找到默认网页目标: {targetName}，且无可用的备选目标。", "配置错误");
            }
        }
    }

    [RelayCommand]
    private async Task OpenWebTarget(WebTarget? target)
    {
        if (target == null || SelectedFile == null) return;

        // 1. Check Variables (if any are empty, stop)
        if (HasVariables)
        {
            foreach (var v in Variables)
            {
                if (string.IsNullOrWhiteSpace(v.Value))
                {
                    _dialogService.ShowAlert("请先填写所有变量值。", "变量未填");
                    return;
                }
            }
        }

        // 2. Compile Content
        var content = CompileContent();
        if (string.IsNullOrWhiteSpace(content))
        {
            _dialogService.ShowAlert("提示词内容为空。", "无法打开");
            return;
        }

        // 3. Determine URL strategy
        bool supportsUrlParam = target.UrlTemplate.Contains("{0}");
        bool useClipboard = !supportsUrlParam || content.Length > 2000;
        bool autoPaste = !supportsUrlParam; // Auto Ctrl+V for clipboard-only targets (e.g. Gemini)
        string url;

        try
        {
            if (useClipboard)
            {
                // Copy to clipboard
                _clipboardService.SetClipboard(content);

                if (supportsUrlParam)
                {
                    // Has {0} but content too long, strip query
                    try { url = string.Format(target.UrlTemplate, ""); }
                    catch { url = target.UrlTemplate.Split('?')[0]; }
                    _dialogService.ShowAlert("提示词过长，已复制到剪贴板，请手动粘贴。", "提示");
                }
                else
                {
                    // No {0} placeholder — use URL as-is (e.g. Gemini)
                    url = target.UrlTemplate;
                    // No dialog here — we will auto-paste after delay
                }
            }
            else
            {
                // URL Encode and format
                url = string.Format(target.UrlTemplate, System.Uri.EscapeDataString(content));
            }

            // 4. Open Browser
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });

            // 5. Hide Window (to Tray)
            if (Application.Current.MainWindow != null)
            {
                Application.Current.MainWindow.Hide();
            }

            // 6. Auto-paste is no longer needed as we use Userscript to handle ?q= parameter
            // The Userscript will intercept the URL, extract the prompt, and fill the input box.

            // 7. Clear Input
            AdditionalInput = "";
        }
        catch (System.Exception ex)
        {
            Infrastructure.Services.LoggerService.Instance.LogException(ex, "OpenWebTarget Failed", "MainViewModel");
            _dialogService.ShowAlert($"打开网页失败: {ex.Message}", "错误");
        }
    }

    [RelayCommand]
    private void SearchOnGitHub()
    {
        // 1. Get content from Block4 input only
        var query = AdditionalInput?.Trim();

        if (string.IsNullOrWhiteSpace(query))
        {
            _dialogService.ShowAlert("请输入要搜索的内容。", "输入为空");
            return;
        }

        try
        {
            // 2. Construct GitHub Search URL
            var url = $"https://github.com/search?q={System.Uri.EscapeDataString(query)}";

            // 3. Open Browser
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });

            // 4. Clear input (consistent with other "send" actions)
            AdditionalInput = "";
        }
        catch (System.Exception ex)
        {
            Infrastructure.Services.LoggerService.Instance.LogException(ex, "SearchOnGitHub Failed", "MainViewModel");
            _dialogService.ShowAlert($"打开 GitHub 失败: {ex.Message}", "错误");
        }
    }



    private void ToggleMainWindow()
    {
        var win = Application.Current.MainWindow as MainWindow;
        if (win == null) return;

        win.ToggleWindowVisibility();
    }

    private static string NormalizeSymbols(string s) =>
        StringUtils.NormalizeSymbols(s);

    // ... (existing code) ...





    private void ParseVariablesRealTime(string content)
    {
        if (string.IsNullOrEmpty(content))
        {
            Variables.Clear();
            HasVariables = false;
            return;
        }

        var matches = VariableRegex.Matches(content);
        var newVarNames = matches.Cast<Match>()
            .Select(m => m.Groups[1].Value.Trim())
            .Where(s => !string.IsNullOrEmpty(s))
            .Distinct()
            .ToList();

        for (int i = Variables.Count - 1; i >= 0; i--)
        {
            if (!newVarNames.Contains(Variables[i].Name)) Variables.RemoveAt(i);
        }

        foreach (var name in newVarNames)
        {
            if (!Variables.Any(v => v.Name == name)) Variables.Add(new VariableItem { Name = name });
        }

        HasVariables = Variables.Count > 0;
    }



    [SupportedOSPlatform("windows")]
    public void UpdateWindowHotkeys()
    {
        _hotkeyService.RegisterWindowHotkey("ToggleFullWindowHotkey", Config.FullWindowHotkey, () => ToggleWindowToMode(true));
        
        // 注册启动条显示/隐藏快捷键
        _hotkeyService.RegisterWindowHotkey("ToggleLaunchBarHotkey", Config.LaunchBarHotkey, () => 
        {
            Config.EnableLaunchBar = !Config.EnableLaunchBar;
        });
    }

    private void ToggleWindowToMode(bool targetFull)
    {
        var win = Application.Current.MainWindow;
        if (win == null) return;

        if (win.Visibility != Visibility.Visible)
        {
            IsFullMode = true;
            win.Show();
            win.Activate();
            return;
        }

        win.Hide();
    }




    public void MoveFileToFolder(PromptItem f, FolderItem t)
    {
        if (f == null || t == null || f.FolderId == t.Id) return;
        f.FolderId = t.Id;
        FilesView?.Refresh();
        if (SelectedFile == f) SelectedFile = null;
        RequestSave();
    }

    [RelayCommand]
    private void RequestSave()
    {
        if (!IsDirty) IsDirty = true;
        _saveSubject.OnNext(System.Reactive.Unit.Default);
    }

        public async Task PerformLocalBackup()
        {
            try
            {
                await _localDataService.SaveAsync(Folders, Files);
            }
            catch (Exception ex)
            {
                LoggerService.Instance.LogException(ex, "Failed to perform local backup", "MainViewModel.PerformLocalBackup");
            }
        }

    /// <summary>
    /// 当 Files 集合发生变化时（添加/删除项目），附加或移除监听器并触发保存
    /// </summary>
    private void OnFilesCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        // 为新添加的项目附加监听器
        if (e.NewItems != null)
        {
            foreach (PromptItem item in e.NewItems)
            {
                item.PropertyChanged += OnFilePropertyChanged;
            }
        }

        // 从移除的项目上卸载监听器
        if (e.OldItems != null)
        {
            foreach (PromptItem item in e.OldItems)
            {
                item.PropertyChanged -= OnFilePropertyChanged;
            }
        }

        // 集合变化时触发保存
        RequestSave();
    }

    /// <summary>
    /// 当 PromptItem 的属性发生变化时触发保存
    /// </summary>
    private void OnFilePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // 避免因更新 LastModified 导致的循环触发
        if (e.PropertyName == nameof(PromptItem.LastModified))
            return;

        if (e.PropertyName == nameof(PromptItem.Content) && sender == SelectedFile)
        {
            ParseVariablesRealTime(SelectedFile?.Content ?? "");
        }

        RequestSave();
    }

    private void UpdateTimeDisplay()
    {
        if (LocalConfig?.LastCloudSyncTime == null)
        {
            SyncTimeDisplay = "-";
            return;
        }

        var diff = DateTime.Now - LocalConfig.LastCloudSyncTime.Value;

        if (diff.TotalSeconds < 60)
        {
            SyncTimeDisplay = $"{(int)diff.TotalSeconds}s";
        }
        else if (diff.TotalMinutes < 60)
        {
            SyncTimeDisplay = $"{(int)diff.TotalMinutes}min";
        }
        else if (diff.TotalHours < 24)
        {
            SyncTimeDisplay = $"{(int)diff.TotalHours}H";
        }
        else
        {
            SyncTimeDisplay = $"{(int)diff.TotalDays}d";
        }
    }

    private string CompileContent()
    {
        string finalContent = SelectedFile?.Content ?? "";

        if (HasVariables)
        {
            foreach (var variable in Variables)
            {
                finalContent = finalContent.Replace("{{" + variable.Name + "}}", variable.Value ?? "");
            }
        }

        if (!string.IsNullOrWhiteSpace(AdditionalInput))
        {
            if (!string.IsNullOrWhiteSpace(finalContent)) finalContent += "\n";
            finalContent += AdditionalInput;
        }

        return finalContent;
    }

    public void HandleLauncherTriggered()
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            // Close any existing launcher windows to avoid stacking
            foreach (Window w in Application.Current.Windows)
            {
                if (w is Views.LauncherWindow)
                {
                    w.Activate();
                    w.Focus();
                    return;
                }
            }

            if (Application.Current is not App app) return;
            
            var vm = app.ServiceProvider.GetRequiredService<LauncherViewModel>();
            var win = new Views.LauncherWindow
            {
                DataContext = vm
            };

            vm.RequestClose = () => win.Close();

            win.Show();
            win.Activate();
            win.Focus();
        });
    }


    // ========== IDisposable ==========
    private bool _disposed = false;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        // Stop and dispose timer
        if (_timer != null)
        {
            _timer.Stop();
            _timer = null!;
        }

        // Dispose Reactive Subjects
        _saveSubject?.Dispose();
        _saveLocalSettingsSubject?.Dispose();

        // Unsubscribe from GlobalKeyService events and dispose
        if (_keyService != null)
        {
            if (_onLauncherTriggeredHandler != null)
                _keyService.OnLauncherTriggered -= _onLauncherTriggeredHandler;
            _keyService.Dispose();
        }

        if (LocalConfig != null && _localConfigPropertyChangedHandler != null)
        {
            LocalConfig.PropertyChanged -= _localConfigPropertyChangedHandler;
        }

        if (Files != null)
        {
            Files.CollectionChanged -= OnFilesCollectionChanged;
            foreach (var item in Files)
            {
                item.PropertyChanged -= OnFilePropertyChanged;
            }
        }

        WeakReferenceMessenger.Default.UnregisterAll(this);

        GC.SuppressFinalize(this);
    }
}

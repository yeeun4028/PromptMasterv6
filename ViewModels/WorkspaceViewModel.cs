using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Markdig;
using PromptMasterv6.Core.Interfaces;
using PromptMasterv6.Core.Models;
using PromptMasterv6.Infrastructure.Services;
using PromptMasterv6.ViewModels.Messages;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using Clipboard = System.Windows.Clipboard;

namespace PromptMasterv6.ViewModels;

public partial class WorkspaceViewModel : ObservableObject
{
    private readonly IDataService _dataService;
    private readonly IAiService _aiService;
    private readonly IDialogService _dialogService;
    private readonly ClipboardService _clipboardService;
    private readonly ISettingsService _settingsService;

    private static readonly Regex VariableRegex = new(@"\{\{(.*?)\}\}", RegexOptions.Compiled);

    [ObservableProperty] private ObservableCollection<PromptItem> files = new();
    [ObservableProperty] private PromptItem? selectedFile;
    [ObservableProperty] private bool isEditMode;
    [ObservableProperty] private string? previewContent;
    [ObservableProperty] private ObservableCollection<VariableItem> variables = new();
    [ObservableProperty] private bool hasVariables;
    [ObservableProperty] private string additionalInput = "";
    [ObservableProperty] private bool isDirty;
    [ObservableProperty] private ICollectionView? filesView;
    [ObservableProperty] private FolderItem? selectedFolder;

    private string? _originalContentBeforeEdit;

    public MarkdownPipeline Pipeline { get; }
    public AppConfig Config => _settingsService.Config;

    partial void OnSelectedFileChanged(PromptItem? value)
    {
        IsEditMode = false;
        PreviewContent = SafeConvertHtmlToMarkdown(value?.Content);
        SafeParseVariables(value?.Content ?? "");
    }

    public WorkspaceViewModel(
        ISettingsService settingsService,
        IDataService dataService,
        IAiService aiService,
        IDialogService dialogService,
        ClipboardService clipboardService)
    {
        _settingsService = settingsService;
        _dataService = dataService;
        _aiService = aiService;
        _dialogService = dialogService;
        _clipboardService = clipboardService;

        Pipeline = new MarkdownPipelineBuilder()
            .UseSoftlineBreakAsHardlineBreak()
            .DisableHtml()
            .Build();

        WeakReferenceMessenger.Default.Register<RequestSelectFileMessage>(this, (_, m) =>
        {
            SelectedFile = m.File;
            if (m.EnterEditMode) IsEditMode = true;
        });

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

        WeakReferenceMessenger.Default.Register<FolderSelectionChangedMessage>(this, (_, _) =>
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

        WeakReferenceMessenger.Default.Register<ReloadDataMessage>(this, async (_, _) =>
        {
            await LoadDataAsync();
        });
    }

    public void SetFiles(ObservableCollection<PromptItem> files)
    {
        Files = files;
        Files.CollectionChanged += OnFilesCollectionChanged;
        foreach (var item in Files)
        {
            item.PropertyChanged += OnFilePropertyChanged;
        }
        FilesView = CollectionViewSource.GetDefaultView(Files);
        UpdateFilesViewFilter();
    }

    public void SetSelectedFolder(FolderItem? folder)
    {
        SelectedFolder = folder;
        UpdateFilesViewFilter();
        FilesView?.Refresh();
    }

    public async Task LoadDataAsync()
    {
        var data = await _dataService.LoadAsync();
        if (data == null) return;

        Files.Clear();
        var files = data.Files ?? new List<PromptItem>();
        foreach (var f in files)
        {
            Files.Add(f);
        }

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
    private void RenameFile(PromptItem? item)
    {
        if (item != null)
        {
            item.IsRenaming = true;
        }
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
        var dialog = new IconInputDialog(file.IconGeometry);
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
            bool contentChanged = !string.Equals(_originalContentBeforeEdit, SelectedFile.Content, StringComparison.Ordinal);

            if (contentChanged)
            {
                SelectedFile.LastModified = DateTime.Now;
                RequestSave();
            }

            IsEditMode = false;
            PreviewContent = ConvertHtmlToMarkdown(SelectedFile.Content);
            _originalContentBeforeEdit = null;
            return;
        }

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

        var content = CompileContent();
        if (string.IsNullOrWhiteSpace(content))
        {
            _dialogService.ShowAlert("提示词内容为空。", "无法打开");
            return;
        }

        bool supportsUrlParam = target.UrlTemplate.Contains("{0}");
        bool useClipboard = !supportsUrlParam || content.Length > 2000;
        bool autoPaste = !supportsUrlParam;
        string url;

        try
        {
            if (useClipboard)
            {
                _clipboardService.SetClipboard(content);

                if (supportsUrlParam)
                {
                    try { url = string.Format(target.UrlTemplate, ""); }
                    catch { url = target.UrlTemplate.Split('?')[0]; }
                    _dialogService.ShowAlert("提示词过长，已复制到剪贴板，请手动粘贴。", "提示");
                }
                else
                {
                    url = target.UrlTemplate;
                }
            }
            else
            {
                url = string.Format(target.UrlTemplate, System.Uri.EscapeDataString(content));
            }

            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });

            if (System.Windows.Application.Current.MainWindow != null)
            {
                System.Windows.Application.Current.MainWindow.Hide();
            }

            AdditionalInput = "";
        }
        catch (System.Exception ex)
        {
            Infrastructure.Services.LoggerService.Instance.LogException(ex, "OpenWebTarget Failed", "WorkspaceViewModel");
            _dialogService.ShowAlert($"打开网页失败: {ex.Message}", "错误");
        }
    }

    [RelayCommand]
    private void SearchOnGitHub()
    {
        var query = AdditionalInput?.Trim();

        if (string.IsNullOrWhiteSpace(query))
        {
            _dialogService.ShowAlert("请输入要搜索的内容。", "输入为空");
            return;
        }

        try
        {
            var url = $"https://github.com/search?q={System.Uri.EscapeDataString(query)}";

            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });

            AdditionalInput = "";
        }
        catch (System.Exception ex)
        {
            Infrastructure.Services.LoggerService.Instance.LogException(ex, "SearchOnGitHub Failed", "WorkspaceViewModel");
            _dialogService.ShowAlert($"打开 GitHub 失败: {ex.Message}", "错误");
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
            var htmlTagRegex = new Regex(
                @"<(p|div|br|span|a|img|ul|ol|li|table|tr|td|th|h[1-6]|strong|em|b|i|u|code|pre|blockquote|script|style|iframe|form|input|button)[\s>/]",
                RegexOptions.IgnoreCase,
                TimeSpan.FromMilliseconds(100)
            );
            return htmlTagRegex.IsMatch(content);
        }
        catch (RegexMatchTimeoutException)
        {
            return false;
        }
    }

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

    [RelayCommand]
    private void RequestSave()
    {
        if (!IsDirty) IsDirty = true;
        WeakReferenceMessenger.Default.Send(new RequestSaveMessage());
    }

    private void OnFilesCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems != null)
        {
            foreach (PromptItem item in e.NewItems)
            {
                item.PropertyChanged += OnFilePropertyChanged;
            }
        }

        if (e.OldItems != null)
        {
            foreach (PromptItem item in e.OldItems)
            {
                item.PropertyChanged -= OnFilePropertyChanged;
            }
        }

        RequestSave();
    }

    private void OnFilePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PromptItem.LastModified))
            return;

        if (e.PropertyName == nameof(PromptItem.Content) && sender == SelectedFile)
        {
            ParseVariablesRealTime(SelectedFile?.Content ?? "");
        }

        RequestSave();
    }
}

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Markdig;
using PromptMasterv6.Core.Interfaces;
using PromptMasterv6.Infrastructure.Services;
using PromptMasterv6.Features.Main.Messages;
using PromptMasterv6.Core.Messages;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;

namespace PromptMasterv6.Features.Workspace
{
    public partial class WorkspaceViewModel : ObservableObject
    {
        private readonly IDataService _dataService;
        private readonly AiService _aiService;
        private readonly DialogService _dialogService;
        private readonly ClipboardService _clipboardService;
        private readonly SettingsService _settingsService;
        private readonly VariableService _variableService;
        private readonly ContentConverterService _contentConverterService;
        private readonly WebTargetService _webTargetService;
        private readonly LoggerService _logger;

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
            PreviewContent = _contentConverterService.ConvertHtmlToMarkdown(value?.Content);
            SafeParseVariables(value?.Content ?? "");
        }

        public WorkspaceViewModel(
            SettingsService settingsService,
            IDataService dataService,
            AiService aiService,
            DialogService dialogService,
            ClipboardService clipboardService,
            VariableService variableService,
            ContentConverterService contentConverterService,
            WebTargetService webTargetService,
            LoggerService logger)
        {
            _settingsService = settingsService;
            _dataService = dataService;
            _aiService = aiService;
            _dialogService = dialogService;
            _clipboardService = clipboardService;
            _variableService = variableService;
            _contentConverterService = contentConverterService;
            _webTargetService = webTargetService;
            _logger = logger;

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

        private void SafeParseVariables(string content)
        {
            try
            {
                _variableService.ParseVariables(content, Variables);
                HasVariables = _variableService.HasVariables(Variables);
            }
            catch (Exception ex)
            {
                _logger.LogException(ex, "变量解析失败", "SafeParseVariables");
            }
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
                PreviewContent = _contentConverterService.ConvertHtmlToMarkdown(SelectedFile.Content);
                _originalContentBeforeEdit = null;
                return;
            }

            _originalContentBeforeEdit = SelectedFile.Content;
            IsEditMode = true;
        }

        [RelayCommand]
        private void CopyCompiledText()
        {
            var text = _variableService.CompileContent(SelectedFile?.Content, Variables, AdditionalInput);
            if (string.IsNullOrWhiteSpace(text)) return;
            _clipboardService.SetClipboard(text);
        }

        [RelayCommand]
        private async Task SendDefaultWebTarget()
        {
            if (SelectedFile == null) return;

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

            var content = _variableService.CompileContent(SelectedFile?.Content, Variables, AdditionalInput);
            await _webTargetService.SendToDefaultTargetAsync(content, Config);
            AdditionalInput = "";
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

            var content = _variableService.CompileContent(SelectedFile?.Content, Variables, AdditionalInput);
            await _webTargetService.OpenWebTargetAsync(target, content);
            AdditionalInput = "";
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
                _logger.LogException(ex, "SearchOnGitHub Failed", "WorkspaceViewModel");
                _dialogService.ShowAlert($"打开 GitHub 失败: {ex.Message}", "错误");
            }
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
                _variableService.ParseVariables(SelectedFile?.Content ?? "", Variables);
                HasVariables = _variableService.HasVariables(Variables);
            }

            RequestSave();
        }
    }
}

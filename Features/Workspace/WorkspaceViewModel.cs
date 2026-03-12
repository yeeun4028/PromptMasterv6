using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Markdig;
using MediatR;
using PromptMasterv6.Core.Interfaces;
using PromptMasterv6.Infrastructure.Services;
using PromptMasterv6.Features.Main.FileManager.Messages;
using PromptMasterv6.Features.Shared.Queries;
using PromptMasterv6.Features.Shared.Commands;
using PromptMasterv6.Core.Messages;
using PromptMasterv6.Features.Workspace.LoadWorkspaceData;
using PromptMasterv6.Features.Workspace.SearchOnGitHub;
using PromptMasterv6.Features.Workspace.ChangeFileIcon;
using PromptMasterv6.Features.Workspace.DeleteFile;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Data;

namespace PromptMasterv6.Features.Workspace
{
    public partial class WorkspaceViewModel : ObservableObject
    {
        private readonly IMediator _mediator;
        private readonly DialogService _dialogService;
        private readonly SettingsService _settingsService;
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
            _ = UpdatePreviewContentAsync(value?.Content);
            SafeParseVariables(value?.Content ?? "");
        }

        private async Task UpdatePreviewContentAsync(string? content)
        {
            PreviewContent = await _mediator.Send(new ConvertHtmlToMarkdownQuery(content));
        }

        public WorkspaceViewModel(
            SettingsService settingsService,
            IMediator mediator,
            DialogService dialogService,
            LoggerService logger)
        {
            _settingsService = settingsService;
            _mediator = mediator;
            _dialogService = dialogService;
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
                if (m.HasReceivedResponse) return;
                var file = Files.FirstOrDefault(f => f.Id == m.PromptId);
                if (file != null)
                {
                    m.Reply(new PromptFileResponseMessage { File = file });
                }
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

        private async void SafeParseVariables(string content)
        {
            try
            {
                var varNames = await _mediator.Send(new ParseVariablesQuery(content));
                
                for (int i = Variables.Count - 1; i >= 0; i--)
                {
                    if (!varNames.Contains(Variables[i].Name)) Variables.RemoveAt(i);
                }

                foreach (var name in varNames)
                {
                    if (!Variables.Any(v => v.Name == name)) Variables.Add(new VariableItem { Name = name });
                }

                HasVariables = Variables.Count > 0;
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
            var result = await _mediator.Send(new LoadWorkspaceDataFeature.Command());

            if (!result.Success || result.Files == null)
            {
                _dialogService.ShowAlert(result.ErrorMessage ?? "加载数据失败", "错误");
                return;
            }

            Files.Clear();
            foreach (var f in result.Files)
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
        private async Task DeleteFile(PromptItem? file)
        {
            if (file == null) return;

            var result = await _mediator.Send(new DeleteFileFeature.Command(file, Files));

            if (result.Success)
            {
                if (result.WasSelected) SelectedFile = null;
                RequestSave();
            }
        }

        [RelayCommand]
        private async Task ChangeFileIcon(PromptItem? file)
        {
            if (file == null) return;

            var result = await _mediator.Send(new ChangeFileIconFeature.Command(file));

            if (result.Success && result.NewIconGeometry != null)
            {
                file.IconGeometry = result.NewIconGeometry;
                RequestSave();
            }
        }

        [RelayCommand]
        private async Task ToggleEditMode()
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
                PreviewContent = await _mediator.Send(new ConvertHtmlToMarkdownQuery(SelectedFile.Content));
                _originalContentBeforeEdit = null;
                return;
            }

            _originalContentBeforeEdit = SelectedFile.Content;
            IsEditMode = true;
        }

        [RelayCommand]
        private async Task CopyCompiledText()
        {
            var variablesDict = Variables.ToDictionary(v => v.Name, v => v.Value ?? "");
            await _mediator.Send(new CopyCompiledTextCommand(SelectedFile?.Content, variablesDict, AdditionalInput));
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

            var variablesDict = Variables.ToDictionary(v => v.Name, v => v.Value ?? "");
            var content = await _mediator.Send(new CompileContentQuery(SelectedFile?.Content, variablesDict, AdditionalInput));
            await _mediator.Send(new SendToDefaultTargetCommand(content, Config.WebDirectTargets, Config.DefaultWebTargetName));
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

            var variablesDict = Variables.ToDictionary(v => v.Name, v => v.Value ?? "");
            var content = await _mediator.Send(new CompileContentQuery(SelectedFile?.Content, variablesDict, AdditionalInput));
            await _mediator.Send(new ExecuteWebTargetCommand(target, content));
            AdditionalInput = "";
        }

        [RelayCommand]
        private async Task SearchOnGitHub()
        {
            var query = AdditionalInput?.Trim();

            if (string.IsNullOrWhiteSpace(query))
            {
                _dialogService.ShowAlert("请输入要搜索的内容。", "输入为空");
                return;
            }

            var result = await _mediator.Send(new SearchOnGitHubFeature.Command(query));

            if (result.Success)
            {
                AdditionalInput = "";
            }
            else
            {
                _dialogService.ShowAlert(result.ErrorMessage ?? "搜索失败", "错误");
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

        private async void OnFilePropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(PromptItem.LastModified))
                return;

            if (e.PropertyName == nameof(PromptItem.Content) && sender == SelectedFile)
            {
                var varNames = await _mediator.Send(new ParseVariablesQuery(SelectedFile?.Content ?? ""));
                
                for (int i = Variables.Count - 1; i >= 0; i--)
                {
                    if (!varNames.Contains(Variables[i].Name)) Variables.RemoveAt(i);
                }

                foreach (var name in varNames)
                {
                    if (!Variables.Any(v => v.Name == name)) Variables.Add(new VariableItem { Name = name });
                }

                HasVariables = Variables.Count > 0;
            }

            RequestSave();
        }
    }
}

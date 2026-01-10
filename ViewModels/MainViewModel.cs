using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.ComponentModel;
using System.Windows;
using System.Collections.Specialized;
using System.Windows.Data;
using System.Threading.Tasks;
using System.Windows.Threading; // 必须引用：用于定时器
using GongSolutions.Wpf.DragDrop;
using PromptMasterv5.Models;
using PromptMasterv5.Services;

namespace PromptMasterv5.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly IDataService _dataService;
        private bool _isCreatingFile = false;
        private DispatcherTimer _timer; // 定时器
        private DateTime _lastSyncTime = DateTime.Now; // 记录上次同步时间点

        [ObservableProperty]
        private AppConfig config;

        [ObservableProperty]
        private bool isSettingsOpen = false;

        // ★★★ 新增：极简时间显示 (例如 "5s", "1m") ★★★
        [ObservableProperty]
        private string syncTimeDisplay = "Now";

        [ObservableProperty]
        private ICollectionView? filesView;

        // 默认为 WebDAV 模式 (由 Config 控制连接细节)
        private bool _useLocalMode = false;

        public IDropTarget FolderDropHandler { get; private set; }

        [ObservableProperty]
        private bool isNavigationVisible = true;

        [ObservableProperty]
        private ObservableCollection<FolderItem> folders = new();

        [ObservableProperty]
        private FolderItem? selectedFolder;

        [ObservableProperty]
        private ObservableCollection<PromptItem> files = new();

        [ObservableProperty]
        private PromptItem? selectedFile;

        [ObservableProperty]
        private bool isEditMode;

        [ObservableProperty]
        private ObservableCollection<VariableItem> variables = new();

        [ObservableProperty]
        private bool hasVariables;

        [ObservableProperty]
        private string additionalInput = "";

        public MainViewModel()
        {
            Config = ConfigService.Load();

            if (_useLocalMode)
                _dataService = new FileDataService();
            else
                _dataService = new WebDavDataService();

            FolderDropHandler = new FolderDropHandler(this);

            // ★★★ 启动定时器：每1秒刷新一次时间显示 ★★★
            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromSeconds(1);
            _timer.Tick += (s, e) => UpdateTimeDisplay();
            _timer.Start();

            _ = InitializeAsync();
        }

        // ★★★ 核心逻辑：计算时间差并格式化 ★★★
        private void UpdateTimeDisplay()
        {
            var span = DateTime.Now - _lastSyncTime;

            if (span.TotalSeconds < 60)
            {
                // 小于1分钟：显示秒 (例如 3s)
                // 如果小于1秒，显示 Now
                int sec = (int)span.TotalSeconds;
                SyncTimeDisplay = sec <= 0 ? "Now" : $"{sec}s";
            }
            else if (span.TotalMinutes < 60)
            {
                // 小于1小时：显示分 (例如 5m)
                SyncTimeDisplay = $"{(int)span.TotalMinutes}m";
            }
            else if (span.TotalHours < 24)
            {
                // 小于24小时：显示小时 (例如 2h)
                SyncTimeDisplay = $"{(int)span.TotalHours}h";
            }
            else
            {
                // 大于1天：显示天 (例如 1d)
                SyncTimeDisplay = $"{(int)span.TotalDays}d";
            }
        }

        private async Task InitializeAsync()
        {
            var data = await _dataService.LoadAsync();

            // 加载成功也算一次同步，重置时间
            _lastSyncTime = DateTime.Now;
            UpdateTimeDisplay();

            if (data.Folders.Count == 0)
            {
                data.Folders.Add(new FolderItem { Name = "我的提示词" });
            }
            Folders = new ObservableCollection<FolderItem>(data.Folders);
            Files = new ObservableCollection<PromptItem>(data.Files);

            var defaultFolderId = Folders.First().Id;
            foreach (var file in Files)
            {
                if (string.IsNullOrEmpty(file.FolderId)) file.FolderId = defaultFolderId;
            }

            var view = CollectionViewSource.GetDefaultView(Files);
            if (view != null)
            {
                view.Filter = FilterFiles;
            }
            FilesView = view;

            Files.CollectionChanged += (s, e) => RequestSave();

            SelectedFolder = Folders.First();
        }

        private async void RequestSave()
        {
            if (string.IsNullOrEmpty(Config.UserName) || string.IsNullOrEmpty(Config.Password)) return;
            await SaveDataAsync();
        }

        private async Task SaveDataAsync()
        {
            // 虽然不显示文字了，但可以通过 SyncTimeDisplay 变成 "..." 来暗示正在同步，
            // 不过为了极简，我们这里保持不变，只在成功后刷新时间。
            try
            {
                await _dataService.SaveAsync(Folders, Files);

                // ★★★ 同步成功：更新时间基准点 ★★★
                _lastSyncTime = DateTime.Now;
                UpdateTimeDisplay();
            }
            catch (Exception ex)
            {
                // 如果出错了，可以在时间位置显示 Err
                SyncTimeDisplay = "Err";
                // 也可以选择弹窗或忽略
                // MessageBox.Show(ex.Message);
            }
        }

        [RelayCommand]
        private void OpenSettings()
        {
            Config = ConfigService.Load();
            IsSettingsOpen = true;
        }

        [RelayCommand]
        private void SaveSettings()
        {
            ConfigService.Save(Config);
            IsSettingsOpen = false;
        }

        [RelayCommand]
        private async Task ManualBackup()
        {
            ConfigService.Save(Config);
            try
            {
                await _dataService.SaveAsync(Folders, Files);
                _lastSyncTime = DateTime.Now; // 更新时间
                MessageBox.Show("成功备份到远程服务器！", "备份成功");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"备份失败：{ex.Message}", "错误");
            }
        }

        [RelayCommand]
        private async Task ManualRestore()
        {
            ConfigService.Save(Config);
            if (MessageBox.Show("确定要从远程恢复吗？\n这将覆盖本地当前所有未保存的修改！", "警告", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.No)
            {
                return;
            }

            try
            {
                var data = await _dataService.LoadAsync();
                if (data.Files.Count == 0 && data.Folders.Count == 0)
                {
                    MessageBox.Show("远程似乎没有数据，或者下载失败。", "提示");
                    return;
                }

                Folders = new ObservableCollection<FolderItem>(data.Folders);
                Files = new ObservableCollection<PromptItem>(data.Files);
                var view = CollectionViewSource.GetDefaultView(Files);
                if (view != null) view.Filter = FilterFiles;
                FilesView = view;
                Files.CollectionChanged += (s, e) => RequestSave();
                SelectedFolder = Folders.FirstOrDefault();

                _lastSyncTime = DateTime.Now; // 更新时间
                MessageBox.Show("成功从远程恢复数据！", "恢复成功");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"恢复失败：{ex.Message}", "错误");
            }
        }

        [RelayCommand]
        private void ToggleNavigation() => IsNavigationVisible = !IsNavigationVisible;

        public void MoveFileToFolder(PromptItem file, FolderItem targetFolder)
        {
            if (file == null || targetFolder == null || file.FolderId == targetFolder.Id) return;
            file.FolderId = targetFolder.Id;
            FilesView?.Refresh();
            if (SelectedFile == file) SelectedFile = null;
            RequestSave();
        }

        partial void OnSelectedFolderChanged(FolderItem? value)
        {
            SelectedFile = null;
            FilesView?.Refresh();
        }

        private bool FilterFiles(object obj)
        {
            if (obj is PromptItem file && SelectedFolder != null)
                return file.FolderId == SelectedFolder.Id;
            return false;
        }

        partial void OnSelectedFileChanged(PromptItem? oldValue, PromptItem? newValue)
        {
            if (oldValue != null) oldValue.PropertyChanged -= SelectedFile_PropertyChanged;
            if (newValue != null)
            {
                newValue.PropertyChanged += SelectedFile_PropertyChanged;
                ParseVariables();
            }
            else
            {
                Variables.Clear();
                HasVariables = false;
            }

            if (_isCreatingFile) return;
            IsEditMode = false;
        }

        private void SelectedFile_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(PromptItem.Content)) ParseVariables();
            RequestSave();
        }

        private void ParseVariables()
        {
            if (SelectedFile == null)
            {
                Variables.Clear();
                HasVariables = false;
                return;
            }
            var content = SelectedFile.Content ?? "";
            var matches = Regex.Matches(content, @"\{\{(.*?)\}\}");
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

        [RelayCommand]
        private void CopyCompiledText()
        {
            if (SelectedFile == null) return;
            string finalContent = SelectedFile.Content ?? "";
            if (HasVariables)
            {
                foreach (var variable in Variables)
                {
                    finalContent = finalContent.Replace("{{" + variable.Name + "}}", variable.Value ?? "");
                }
            }
            else if (!string.IsNullOrWhiteSpace(AdditionalInput))
            {
                finalContent += "\n" + AdditionalInput;
            }
            if (!string.IsNullOrEmpty(finalContent)) Clipboard.SetText(finalContent);
        }

        [RelayCommand]
        private void ToggleEditMode()
        {
            IsEditMode = !IsEditMode;
            if (!IsEditMode) RequestSave();
        }

        [RelayCommand]
        private void CreateFolder()
        {
            var newFolder = new FolderItem { Name = $"新建文件夹 {Folders.Count + 1}" };
            Folders.Add(newFolder);
            SelectedFolder = newFolder;
            RequestSave();
        }

        [RelayCommand]
        private void CreateFile()
        {
            if (SelectedFolder == null) return;
            _isCreatingFile = true;
            var newFile = new PromptItem
            {
                Title = "新文档",
                Content = "# 新文档\n你好，我是{{name}}...",
                LastModified = DateTime.Now,
                FolderId = SelectedFolder.Id
            };
            Files.Add(newFile);
            SelectedFile = newFile;
            IsEditMode = true;
            RequestSave();
            _isCreatingFile = false;
        }

        [RelayCommand]
        private void DeleteFile(PromptItem? item)
        {
            var target = item ?? SelectedFile;
            if (target != null)
            {
                Files.Remove(target);
                if (SelectedFile == target) SelectedFile = null;
                RequestSave();
            }
        }
    }
}
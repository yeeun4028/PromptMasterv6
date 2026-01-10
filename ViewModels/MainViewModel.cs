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
using System.Windows.Threading;
using System.Windows.Input; // 引用 Key, ModifierKeys
using GongSolutions.Wpf.DragDrop;
using NHotkey; // ★ 必须引用
using NHotkey.Wpf; // ★ 必须引用
using PromptMasterv5.Models;
using PromptMasterv5.Services;

namespace PromptMasterv5.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly IDataService _dataService;
        private bool _isCreatingFile = false;
        private DispatcherTimer _timer;
        private DateTime _lastSyncTime = DateTime.Now;

        [ObservableProperty]
        private AppConfig config;

        [ObservableProperty]
        private bool isSettingsOpen = false;

        // 0=同步, 1=快捷键
        [ObservableProperty]
        private int selectedSettingsTab = 0;

        [ObservableProperty]
        private string syncTimeDisplay = "Now";

        [ObservableProperty]
        private ICollectionView? filesView;

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

            // ★★★ 启动时注册热键 ★★★
            UpdateGlobalHotkey();

            if (_useLocalMode)
                _dataService = new FileDataService();
            else
                _dataService = new WebDavDataService();

            FolderDropHandler = new FolderDropHandler(this);

            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromSeconds(1);
            _timer.Tick += (s, e) => UpdateTimeDisplay();
            _timer.Start();

            _ = InitializeAsync();
        }

        // ★★★ 核心：注册/更新全局热键逻辑 ★★★
        public void UpdateGlobalHotkey()
        {
            try
            {
                string hotkeyStr = Config.GlobalHotkey;
                if (string.IsNullOrEmpty(hotkeyStr)) return;

                // 解析逻辑：例如 "Ctrl+Alt+Space"
                ModifierKeys modifiers = ModifierKeys.None;
                if (hotkeyStr.Contains("Ctrl")) modifiers |= ModifierKeys.Control;
                if (hotkeyStr.Contains("Alt")) modifiers |= ModifierKeys.Alt;
                if (hotkeyStr.Contains("Shift")) modifiers |= ModifierKeys.Shift;
                if (hotkeyStr.Contains("Win")) modifiers |= ModifierKeys.Windows;

                // 获取最后一个按键 (例如 "Space")
                string keyStr = hotkeyStr.Split('+').Last().Trim();
                if (Enum.TryParse(keyStr, out Key key))
                {
                    // 先移除旧的，防止冲突，然后注册新的
                    HotkeyManager.Current.AddOrReplace("ToggleWindow", key, modifiers, OnGlobalHotkeyTriggered);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"热键注册失败: {ex.Message}");
            }
        }

        // ★★★ 热键触发时的动作：显示/隐藏主窗口 ★★★
        private void OnGlobalHotkeyTriggered(object sender, HotkeyEventArgs e)
        {
            var window = Application.Current.MainWindow;
            if (window == null) return;

            if (window.Visibility == Visibility.Visible)
            {
                if (window.WindowState == WindowState.Minimized)
                {
                    window.Show();
                    window.WindowState = WindowState.Normal;
                    window.Activate();
                }
                else if (window.IsActive) // 如果当前正是活跃状态，则隐藏
                {
                    window.WindowState = WindowState.Minimized;
                }
                else
                {
                    window.Activate(); // 如果被遮挡，则置顶
                }
            }
            else
            {
                window.Show();
                window.WindowState = WindowState.Normal;
                window.Activate();
            }
        }

        private void UpdateTimeDisplay()
        {
            var span = DateTime.Now - _lastSyncTime;
            if (span.TotalSeconds < 60)
            {
                int sec = (int)span.TotalSeconds;
                SyncTimeDisplay = sec <= 0 ? "Now" : $"{sec}s";
            }
            else if (span.TotalMinutes < 60)
            {
                SyncTimeDisplay = $"{(int)span.TotalMinutes}m";
            }
            else if (span.TotalHours < 24)
            {
                SyncTimeDisplay = $"{(int)span.TotalHours}h";
            }
            else
            {
                SyncTimeDisplay = $"{(int)span.TotalDays}d";
            }
        }

        private async Task InitializeAsync()
        {
            var data = await _dataService.LoadAsync();
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
            if (view != null) view.Filter = FilterFiles;
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
            try
            {
                await _dataService.SaveAsync(Folders, Files);
                _lastSyncTime = DateTime.Now;
                UpdateTimeDisplay();
            }
            catch (Exception)
            {
                SyncTimeDisplay = "Err";
            }
        }

        [RelayCommand]
        private void OpenSettings()
        {
            Config = ConfigService.Load();
            SelectedSettingsTab = 0;
            IsSettingsOpen = true;
        }

        [RelayCommand]
        private void SaveSettings()
        {
            ConfigService.Save(Config);
            // ★ 保存时立即应用新热键
            UpdateGlobalHotkey();
            IsSettingsOpen = false;
        }

        [RelayCommand]
        private void SelectSettingsTab(string indexStr)
        {
            if (int.TryParse(indexStr, out int index))
            {
                SelectedSettingsTab = index;
            }
        }

        [RelayCommand]
        private async Task ManualBackup()
        {
            ConfigService.Save(Config);
            try
            {
                await _dataService.SaveAsync(Folders, Files);
                _lastSyncTime = DateTime.Now;
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

                _lastSyncTime = DateTime.Now;
                MessageBox.Show("成功从远程恢复数据！", "恢复成功");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"恢复失败：{ex.Message}", "错误");
            }
        }

        [RelayCommand]
        private void ToggleNavigation() => IsNavigationVisible = !IsNavigationVisible;

        public void ReorderFolders(int oldIndex, int newIndex)
        {
            Folders.Move(oldIndex, newIndex);
            RequestSave();
        }

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
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.ComponentModel;
using System.Windows;
using System.Collections.Specialized;
using System.Windows.Data; // 引用 ICollectionView
using PromptMasterv5.Models;
using PromptMasterv5.Services;

namespace PromptMasterv5.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly IDataService _dataService;
        private bool _isCreatingFile = false;

        // ★ 新增：列表视图，用于过滤显示
        public ICollectionView FilesView { get; private set; }

        [ObservableProperty]
        private ObservableCollection<FolderItem> folders;

        // ★ 新增：当前选中的文件夹
        [ObservableProperty]
        private FolderItem? selectedFolder;

        [ObservableProperty]
        private ObservableCollection<PromptItem> files;

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
            _dataService = new FileDataService();
            var data = _dataService.Load();

            // 初始化文件夹
            if (data.Folders.Count == 0)
            {
                data.Folders.Add(new FolderItem { Name = "我的提示词" });
            }
            Folders = new ObservableCollection<FolderItem>(data.Folders);

            // 初始化文件
            Files = new ObservableCollection<PromptItem>(data.Files);

            // 数据清洗：如果有老数据没有 FolderId，默认分配给第一个文件夹
            var defaultFolderId = Folders.First().Id;
            foreach (var file in Files)
            {
                if (string.IsNullOrEmpty(file.FolderId))
                {
                    file.FolderId = defaultFolderId;
                }
            }

            // ★ 初始化视图过滤机制
            FilesView = CollectionViewSource.GetDefaultView(Files);
            FilesView.Filter = FilterFiles; // 指定过滤函数

            // 监听文件列表变化（拖拽排序用）
            Files.CollectionChanged += Files_CollectionChanged;

            // 默认选中第一个文件夹
            SelectedFolder = Folders.First();
        }

        // ★ 当选中的文件夹改变时，刷新右侧文件列表
        partial void OnSelectedFolderChanged(FolderItem? value)
        {
            SelectedFile = null; // 切换文件夹时清空选中的文件
            FilesView.Refresh(); // 触发过滤
        }

        // ★ 核心过滤逻辑：只显示 FolderId 匹配的文件
        private bool FilterFiles(object obj)
        {
            if (obj is PromptItem file && SelectedFolder != null)
            {
                return file.FolderId == SelectedFolder.Id;
            }
            return false;
        }

        private void Files_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            SaveData();
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
            SaveData();
        }

        private void SelectedFile_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(PromptItem.Content))
            {
                ParseVariables();
            }
            SaveData();
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
                if (!newVarNames.Contains(Variables[i].Name))
                {
                    Variables.RemoveAt(i);
                }
            }

            foreach (var name in newVarNames)
            {
                if (!Variables.Any(v => v.Name == name))
                {
                    Variables.Add(new VariableItem { Name = name });
                }
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
                    string placeholder = "{{" + variable.Name + "}}";
                    string value = variable.Value ?? "";
                    finalContent = finalContent.Replace(placeholder, value);
                }
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(AdditionalInput))
                {
                    finalContent += "\n" + AdditionalInput;
                }
            }

            if (!string.IsNullOrEmpty(finalContent))
            {
                Clipboard.SetText(finalContent);
            }
        }

        [RelayCommand]
        private void ToggleEditMode()
        {
            IsEditMode = !IsEditMode;
            if (!IsEditMode) SaveData();
        }

        [RelayCommand]
        private void CreateFolder()
        {
            var newFolder = new FolderItem { Name = $"新建文件夹 {Folders.Count + 1}" };
            Folders.Add(newFolder);
            SelectedFolder = newFolder; // 选中新文件夹
            SaveData();
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
                FolderId = SelectedFolder.Id // ★ 关键：标记归属
            };

            Files.Add(newFile);
            SelectedFile = newFile;
            IsEditMode = true;

            SaveData();
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
                SaveData();
            }
        }

        private void SaveData()
        {
            _dataService.Save(Folders, Files);
        }
    }
}
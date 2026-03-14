using CommunityToolkit.Mvvm.ComponentModel;
using PromptMasterv6.Features.Shared.Models;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace PromptMasterv6.Features.Workspace.State;

public partial class WorkspaceState : ObservableObject, IWorkspaceState
{
    [ObservableProperty]
    private FolderItem? _selectedFolder;

    [ObservableProperty]
    private PromptItem? _selectedFile;

    [ObservableProperty]
    private bool _isDirty;

    [ObservableProperty]
    private bool _isEditMode;

    [ObservableProperty]
    private string? _previewContent;

    [ObservableProperty]
    private bool _hasVariables;

    [ObservableProperty]
    private string _additionalInput = "";

    [ObservableProperty]
    private ICollectionView? _filesView;

    [ObservableProperty]
    private ObservableCollection<WebTarget> _webDirectTargets = new();

    [ObservableProperty]
    private string? _defaultWebTargetName;

    public ObservableCollection<FolderItem> Folders { get; } = new();
    public ObservableCollection<PromptItem> Files { get; } = new();
    public ObservableCollection<VariableItem> Variables { get; } = new();

    public void Initialize(ObservableCollection<FolderItem> folders, ObservableCollection<PromptItem> files)
    {
        Folders.Clear();
        foreach (var folder in folders)
        {
            Folders.Add(folder);
        }

        Files.Clear();
        foreach (var file in files)
        {
            Files.Add(file);
        }
    }
}

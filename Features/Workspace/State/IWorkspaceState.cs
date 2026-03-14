using PromptMasterv6.Features.Shared.Models;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace PromptMasterv6.Features.Workspace.State;

public interface IWorkspaceState
{
    ObservableCollection<FolderItem> Folders { get; }
    ObservableCollection<PromptItem> Files { get; }
    FolderItem? SelectedFolder { get; set; }
    PromptItem? SelectedFile { get; set; }
    bool IsDirty { get; set; }
    
    bool IsEditMode { get; set; }
    string? PreviewContent { get; set; }
    ObservableCollection<VariableItem> Variables { get; }
    bool HasVariables { get; set; }
    string AdditionalInput { get; set; }
    ICollectionView? FilesView { get; set; }
    
    void Initialize(ObservableCollection<FolderItem> folders, ObservableCollection<PromptItem> files);
}

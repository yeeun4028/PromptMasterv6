using PromptMasterv6.Features.Shared.Models;
using System.Collections.ObjectModel;

namespace PromptMasterv6.Features.Workspace.State;

public interface IWorkspaceState
{
    ObservableCollection<FolderItem> Folders { get; }
    ObservableCollection<PromptItem> Files { get; }
    FolderItem? SelectedFolder { get; set; }
    PromptItem? SelectedFile { get; set; }
    bool IsDirty { get; set; }
    
    void Initialize(ObservableCollection<FolderItem> folders, ObservableCollection<PromptItem> files);
}

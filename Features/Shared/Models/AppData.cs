using PromptMasterv6.Features.Settings.ApiCredentials;
using PromptMasterv6.Features.Settings.AiModels;

namespace PromptMasterv6.Features.Shared.Models;

public class AppData
{
    public List<FolderItem> Folders { get; set; } = new();
    public List<PromptItem> Files { get; set; } = new();
    public List<ApiProfile> ApiProfiles { get; set; } = new();
    public List<AiModelConfig> SavedModels { get; set; } = new();
}

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace PromptMasterv6.Core.Models
{
    public class AppData
    {
        public List<FolderItem> Folders { get; set; } = new();
        public List<PromptItem> Files { get; set; } = new();
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Dictionary<string, string>? VoiceCommands { get; set; } = null;
        public Dictionary<string, VoiceCommand> VoiceCommandsV2 { get; set; } = new();
        public Dictionary<string, string> VoiceCommandIntentCache { get; set; } = new();
        public List<ApiProfile> ApiProfiles { get; set; } = new();
        public List<AiModelConfig> SavedModels { get; set; } = new();
    }
}

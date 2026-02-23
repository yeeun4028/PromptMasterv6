using PromptMasterv5.Core.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PromptMasterv5.Core.Interfaces
{
    public interface IDataService
    {
        Task<AppData> LoadAsync();
        Task SaveAsync(IEnumerable<FolderItem> folders, IEnumerable<PromptItem> files, Dictionary<string, string> voiceCommands);
        
    }
}
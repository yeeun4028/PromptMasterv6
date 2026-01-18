using PromptMasterv5.Core.Models; // 命名空间记得修改
using PromptMasterv5.Models;
using PromptMasterv5.Services;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PromptMasterv5.Core.Interfaces
{
    public interface IDataService
    {
        Task<AppData> LoadAsync();
        Task SaveAsync(IEnumerable<FolderItem> folders, IEnumerable<PromptItem> files);
    }
}
using PromptMasterv6.Core.Models;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace PromptMasterv6.Core.Interfaces
{
    public interface IDataService
    {
        Task<AppData> LoadAsync(CancellationToken cancellationToken = default);
        Task SaveAsync(IEnumerable<FolderItem> folders, IEnumerable<PromptItem> files, CancellationToken cancellationToken = default);
    }
}

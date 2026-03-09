using PromptMasterv6.Core.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PromptMasterv6.Features.Launcher
{
    public interface ILauncherService
    {
        Task<List<LauncherItem>> GetItemsAsync(IEnumerable<string> paths);
        void ClearCache();
    }
}

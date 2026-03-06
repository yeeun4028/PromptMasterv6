using PromptMasterv6.Core.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PromptMasterv6.Core.Interfaces
{
    public interface ILauncherService
    {
        /// <summary>
        /// Scans the provided directories for launcher items (shortcuts, executables, etc.)
        /// </summary>
        Task<List<LauncherItem>> GetItemsAsync(IEnumerable<string> paths);
        
        /// <summary>
        /// Clears internal cache if any
        /// </summary>
        void ClearCache();
    }
}

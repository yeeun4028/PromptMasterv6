using PromptMasterv6.Core.Models;
using System.Threading.Tasks;

namespace PromptMasterv6.Core.Interfaces
{
    public interface IWebTargetService
    {
        Task OpenWebTargetAsync(WebTarget target, string content, bool hideMainWindow = true);
        Task SendToDefaultTargetAsync(string content, AppConfig config);
    }
}

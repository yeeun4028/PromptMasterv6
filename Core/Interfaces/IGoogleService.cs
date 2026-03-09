using System.Threading.Tasks;
using PromptMasterv6.Core.Models;

namespace PromptMasterv6.Core.Interfaces
{
    public interface IGoogleService
    {
        Task<string> TranslateAsync(string text, ApiProfile profile);
    }
}

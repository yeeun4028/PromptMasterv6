using System.Threading.Tasks;

namespace PromptMasterv6.Core.Interfaces
{
    public interface IGoogleService
    {
        Task<string> TranslateAsync(string text, ApiProfile profile);
    }
}

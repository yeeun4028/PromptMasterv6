using System.Threading.Tasks;
using PromptMasterv6.Core.Models;

namespace PromptMasterv6.Core.Interfaces
{
    public interface ITencentService
    {
        Task<string> TranslateAsync(string content, ApiProfile profile, string from = "auto", string to = "zh");
        Task<string> OcrAsync(byte[] imageBytes, ApiProfile profile);
    }
}

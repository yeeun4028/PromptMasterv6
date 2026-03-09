using System.Threading;
using System.Threading.Tasks;
using PromptMasterv6.Core.Models;

namespace PromptMasterv6.Core.Interfaces
{
    public interface IBaiduService
    {
        Task<string> TranslateAsync(string content, ApiProfile profile, string from = "auto", string to = "zh", CancellationToken cancellationToken = default);
        Task<string> OcrAsync(byte[] imageBytes, ApiProfile profile, string languageType = "CHN_ENG", CancellationToken cancellationToken = default);
    }
}

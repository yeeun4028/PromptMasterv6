using System.Threading.Tasks;

namespace PromptMasterv6.Core.Interfaces
{
    public interface IOcrService
    {
        Task<string> RecognizeTextAsync(string apiKey, string secretKey, byte[] imageBytes);
    }
}
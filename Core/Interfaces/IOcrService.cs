using System.Threading.Tasks;

namespace PromptMasterv5.Core.Interfaces
{
    public interface IOcrService
    {
        Task<string> RecognizeTextAsync(string apiKey, string secretKey, byte[] imageBytes);
    }
}
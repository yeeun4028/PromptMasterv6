using PromptMasterv5.Core.Models;
using System.Threading.Tasks;

namespace PromptMasterv5.Core.Interfaces
{
    public interface IAiService
    {
        Task<string> ChatAsync(string userContent, AppConfig config, string? systemPrompt = null);
        Task<(bool Success, string Message)> TestConnectionAsync(AppConfig config);
    }
}
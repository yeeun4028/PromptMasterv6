using PromptMasterv5.Core.Models;
using System.Threading.Tasks;

namespace PromptMasterv5.Core.Interfaces
{
    public interface IAiService
    {
        Task<string> ChatAsync(string userContent, AppConfig config, string? systemPrompt = null);
        Task<string> ChatAsync(string userContent, string apiKey, string baseUrl, string model, string? systemPrompt = null);
        Task<(bool Success, string Message)> TestConnectionAsync(AppConfig config);
        Task<(bool Success, string Message)> TestConnectionAsync(string apiKey, string baseUrl, string model);
    }
}
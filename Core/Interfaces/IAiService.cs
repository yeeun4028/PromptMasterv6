using PromptMasterv5.Core.Models;
using System.Collections.Generic;
using System.Threading.Tasks;
using OpenAI.ObjectModels.RequestModels;

namespace PromptMasterv5.Core.Interfaces
{
    public interface IAiService
    {
        Task<string> ChatAsync(string userContent, AppConfig config, string? systemPrompt = null);
        Task<string> ChatAsync(string userContent, string apiKey, string baseUrl, string model, string? systemPrompt = null);
        Task<string> ChatWithImageAsync(byte[] imageBytes, string apiKey, string baseUrl, string model, string? systemPrompt = null);
        
        // Streaming support
        IAsyncEnumerable<string> ChatStreamAsync(string userContent, AppConfig config, string? systemPrompt = null);
        IAsyncEnumerable<string> ChatStreamAsync(string userContent, string apiKey, string baseUrl, string model, string? systemPrompt = null);
        IAsyncEnumerable<string> ChatStreamAsync(List<ChatMessage> messages, AppConfig config);
        IAsyncEnumerable<string> ChatStreamAsync(List<ChatMessage> messages, string apiKey, string baseUrl, string model);

        Task<(bool Success, string Message, long? ResponseTimeMs)> TestConnectionAsync(AppConfig config);
        Task<(bool Success, string Message, long? ResponseTimeMs)> TestConnectionAsync(string apiKey, string baseUrl, string model);
    }
}
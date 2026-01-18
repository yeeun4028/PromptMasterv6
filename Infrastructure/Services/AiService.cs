using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using OpenAI;
using OpenAI.Managers;
using OpenAI.ObjectModels.RequestModels;
using OpenAI.ObjectModels;
using PromptMasterv5.Core.Models;
using PromptMasterv5.Core.Interfaces;

namespace PromptMasterv5.Infrastructure.Services
{
    public class AiService : IAiService
    {
        public async Task<string> ChatAsync(string userContent, AppConfig config, string? systemPrompt = null)
        {
            if (string.IsNullOrWhiteSpace(config.AiApiKey))
            {
                return "[设置错误] 请先在设置中配置 AI API Key";
            }

            var options = new OpenAiOptions
            {
                ApiKey = config.AiApiKey,
                BaseDomain = config.AiBaseUrl
            };

            var openAiService = new OpenAIService(options);

            string finalSystemPrompt = systemPrompt ?? "You are a helpful assistant. Output result directly without unnecessary conversational filler. IMPORTANT: Always answer in Simplified Chinese unless the user explicitly asks for another language.";

            var messages = new List<ChatMessage>
            {
                ChatMessage.FromSystem(finalSystemPrompt),
                ChatMessage.FromUser(userContent)
            };

            var request = new ChatCompletionCreateRequest
            {
                Messages = messages,
                Model = config.AiModel,
                Temperature = 0.7f
            };

            try
            {
                var completionResult = await openAiService.ChatCompletion.CreateCompletion(request);

                if (completionResult.Successful)
                {
                    var choice = completionResult.Choices?.FirstOrDefault();
                    if (choice != null && choice.Message != null && !string.IsNullOrEmpty(choice.Message.Content))
                    {
                        return choice.Message.Content.Trim();
                    }
                    return "[AI 无响应] 返回内容为空";
                }
                else
                {
                    if (completionResult.Error == null) return "[AI 错误] 未知网络错误";
                    return $"[AI 错误] {completionResult.Error.Message} ({completionResult.Error.Type})";
                }
            }
            catch (Exception ex)
            {
                return $"[系统错误] {ex.Message}";
            }
        }

        public async Task<(bool Success, string Message)> TestConnectionAsync(AppConfig config)
        {
            if (string.IsNullOrWhiteSpace(config.AiApiKey))
                return (false, "API Key 为空");
            if (string.IsNullOrWhiteSpace(config.AiBaseUrl))
                return (false, "API 地址为空");
            if (string.IsNullOrWhiteSpace(config.AiModel))
                return (false, "模型名称为空");

            try
            {
                var options = new OpenAiOptions
                {
                    ApiKey = config.AiApiKey,
                    BaseDomain = config.AiBaseUrl
                };

                var openAiService = new OpenAIService(options);

                var request = new ChatCompletionCreateRequest
                {
                    Messages = new List<ChatMessage>
                    {
                        ChatMessage.FromSystem("Test"),
                        ChatMessage.FromUser("Hi")
                    },
                    Model = config.AiModel,
                    MaxTokens = 5
                };

                var completionResult = await openAiService.ChatCompletion.CreateCompletion(request);

                if (completionResult.Successful)
                {
                    return (true, "连接成功！");
                }
                else
                {
                    return (false, $"连接失败: {completionResult.Error?.Message ?? "未知错误"}");
                }
            }
            catch (Exception ex)
            {
                return (false, $"连接异常: {ex.Message}");
            }
        }
    }
}

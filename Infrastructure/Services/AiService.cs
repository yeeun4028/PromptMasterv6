using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using OpenAI;
using OpenAI.Managers;
using OpenAI.ObjectModels.RequestModels;
using OpenAI.ObjectModels;
using PromptMasterv5.Core.Models;
using PromptMasterv5.Core.Interfaces;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;

namespace PromptMasterv5.Infrastructure.Services
{
    public class AiService : IAiService
    {
        private readonly IHttpClientFactory _httpClientFactory;

        // 缓存 OpenAIService 实例，避免每次请求都创建新实例
        // 使用 (apiKey + baseUrl) 作为缓存键
        // 限制最大缓存数量，防止内存无限增长
        private const int MaxCacheSize = 10;
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, OpenAIService> _serviceCache 
            = new System.Collections.Concurrent.ConcurrentDictionary<string, OpenAIService>();
        private static readonly object _cacheLock = new();

        public AiService(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        /// <summary>
        /// 获取缓存的 OpenAIService 实例，如果不存在则创建并缓存
        /// </summary>
        private OpenAIService GetOrCreateOpenAiService(string apiKey, string baseUrl)
        {
            string cacheKey = $"{apiKey}|{baseUrl}";
            
            // 如果缓存已满，清理最旧的条目
            if (_serviceCache.Count >= MaxCacheSize)
            {
                lock (_cacheLock)
                {
                    if (_serviceCache.Count >= MaxCacheSize)
                    {
                        // 移除一个条目（简单策略：移除第一个）
                        var firstKey = _serviceCache.Keys.FirstOrDefault();
                        if (firstKey != null && _serviceCache.TryRemove(firstKey, out var oldService))
                        {
                            LoggerService.Instance.LogInfo($"Cache full, removed service for: {firstKey.Substring(0, Math.Min(20, firstKey.Length))}...", "AiService.GetOrCreateOpenAiService");
                        }
                    }
                }
            }
            
            return _serviceCache.GetOrAdd(cacheKey, key => CreateOpenAiServiceInternal(apiKey, baseUrl));
        }

        /// <summary>
        /// 内部方法：创建新的 OpenAIService 实例
        /// </summary>
        private OpenAIService CreateOpenAiServiceInternal(string apiKey, string baseUrl)
        {
            LoggerService.Instance.LogInfo($"Creating new OpenAiService: BaseUrl={baseUrl}", "AiService.CreateOpenAiServiceInternal");
            
            var options = new OpenAiOptions
            {
                ApiKey = apiKey,
                BaseDomain = baseUrl
            };

            // 使用 IHttpClientFactory 创建 HttpClient
            var httpClient = _httpClientFactory.CreateClient("AiServiceClient");
            httpClient.Timeout = TimeSpan.FromMinutes(2);

            return new OpenAIService(options, httpClient);
        }

        public Task<string> ChatAsync(string userContent, AppConfig config, string? systemPrompt = null)
        {
            string apiKey = config.AiApiKey;
            string baseUrl = config.AiBaseUrl;
            string model = config.AiModel;

            if (!string.IsNullOrEmpty(config.ActiveModelId))
            {
                var savedModel = config.SavedModels.FirstOrDefault(m => m.Id == config.ActiveModelId);
                if (savedModel != null)
                {
                    apiKey = savedModel.ApiKey;
                    baseUrl = savedModel.BaseUrl;
                    model = savedModel.ModelName;
                }
            }

            return ChatAsync(userContent, apiKey, baseUrl, model, systemPrompt);
        }

        public async Task<string> ChatAsync(string userContent, string apiKey, string baseUrl, string model, string? systemPrompt = null)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                return "[设置错误] 请先在设置中配置 API Key";
            }

            var openAiService = GetOrCreateOpenAiService(apiKey, baseUrl);

            string finalSystemPrompt = systemPrompt ?? "You are a helpful assistant. Output result directly without unnecessary conversational filler. IMPORTANT: Always answer in Simplified Chinese unless the user explicitly asks for another language.";

            var messages = new List<ChatMessage>
            {
                ChatMessage.FromSystem(finalSystemPrompt),
                ChatMessage.FromUser(userContent)
            };

            var request = new ChatCompletionCreateRequest
            {
                Messages = messages,
                Model = model,
                Temperature = 0.7f
            };

            try
            {
                var completionResult = await openAiService.ChatCompletion.CreateCompletion(request).ConfigureAwait(false);

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

        public async Task<string> ChatWithImageAsync(byte[] imageBytes, string apiKey, string baseUrl, string model, string? systemPrompt = null)
        {
            if (string.IsNullOrWhiteSpace(apiKey)) return "[设置错误] 请先配置 API Key";
            if (imageBytes == null || imageBytes.Length == 0) return "[输入错误] 图片数据为空";

            var openAiService = GetOrCreateOpenAiService(apiKey, baseUrl);

            string finalSystemPrompt = systemPrompt ?? "You are a helpful assistant. Please perform OCR on the provided image.";
            string base64Image = Convert.ToBase64String(imageBytes);
            string imageUrl = $"data:image/jpeg;base64,{base64Image}";

            var messages = new List<ChatMessage>
            {
                ChatMessage.FromSystem(finalSystemPrompt),
                ChatMessage.FromUser(
                    new List<MessageContent>
                    {
                        MessageContent.ImageUrlContent(imageUrl),
                        MessageContent.TextContent("Please identify all text in this image and output it directly.")
                    })
            };

            var request = new ChatCompletionCreateRequest
            {
                Messages = messages,
                Model = model,
                Temperature = 0.3f, // Lower temperature for OCR accuracy
                MaxTokens = 2000
            };

            try
            {
                var completionResult = await openAiService.ChatCompletion.CreateCompletion(request).ConfigureAwait(false);
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

        public IAsyncEnumerable<string> ChatStreamAsync(string userContent, AppConfig config, string? systemPrompt = null)
        {
            string apiKey = config.AiApiKey;
            string baseUrl = config.AiBaseUrl;
            string model = config.AiModel;

            if (!string.IsNullOrEmpty(config.ActiveModelId))
            {
                var savedModel = config.SavedModels.FirstOrDefault(m => m.Id == config.ActiveModelId);
                if (savedModel != null)
                {
                    apiKey = savedModel.ApiKey;
                    baseUrl = savedModel.BaseUrl;
                    model = savedModel.ModelName;
                }
            }

            return ChatStreamAsync(userContent, apiKey, baseUrl, model, systemPrompt);
        }

        public async IAsyncEnumerable<string> ChatStreamAsync(string userContent, string apiKey, string baseUrl, string model, string? systemPrompt = null)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                yield return "[设置错误] 请先在设置中配置 API Key";
                yield break;
            }

            var openAiService = GetOrCreateOpenAiService(apiKey, baseUrl);

            string finalSystemPrompt = systemPrompt ?? "You are a helpful assistant. Output result directly without unnecessary conversational filler. IMPORTANT: Always answer in Simplified Chinese unless the user explicitly asks for another language.";

            var messages = new List<ChatMessage>
            {
                ChatMessage.FromSystem(finalSystemPrompt),
                ChatMessage.FromUser(userContent)
            };

            var request = new ChatCompletionCreateRequest
            {
                Messages = messages,
                Model = model,
                Temperature = 0.7f,
                MaxTokens = 4096, // GLM requires this parameter
                Stream = true // Enable streaming
            };

            // Enhanced logging for debugging
            LoggerService.Instance.LogInfo($"Starting stream request: Model={model}, BaseUrl={baseUrl}, MessageCount={messages.Count}, MaxTokens=4096", "AiService.ChatStreamAsync");

            await foreach (var completionResult in openAiService.ChatCompletion.CreateCompletionAsStream(request).ConfigureAwait(false))
            {
                if (completionResult.Successful)
                {
                    var choice = completionResult.Choices?.FirstOrDefault();
                    if (choice != null && choice.Message != null && !string.IsNullOrEmpty(choice.Message.Content))
                    {
                        yield return choice.Message.Content;
                    }
                }
                else
                {
                    if (completionResult.Error != null)
                    {
                        // Handle null/empty error properties (common with GLM models)
                        string errorMessage = !string.IsNullOrWhiteSpace(completionResult.Error.Message) 
                            ? completionResult.Error.Message 
                            : "未知错误";
                        string errorType = !string.IsNullOrWhiteSpace(completionResult.Error.Type) 
                            ? completionResult.Error.Type 
                            : "Unknown";
                        string errorCode = !string.IsNullOrWhiteSpace(completionResult.Error.Code) 
                            ? completionResult.Error.Code 
                            : "N/A";
                        
                        var errorMsg = $"[AI 错误] {errorMessage} (Type: {errorType}, Code: {errorCode})";
                        LoggerService.Instance.LogError($"Stream Error: {errorMsg}, Full Error Object: Type={completionResult.Error.Type}, Code={completionResult.Error.Code}, Message={completionResult.Error.Message}", "AiService.ChatStreamAsync");
                        yield return errorMsg;
                    }
                    else
                    {
                         LoggerService.Instance.LogError("Stream Error: Unknown error (Successful=false, Error=null)", "AiService.ChatStreamAsync");
                         yield return "[AI 错误] 未知错误";
                    }
                }
            }
        }

        public IAsyncEnumerable<string> ChatStreamAsync(List<ChatMessage> messages, AppConfig config)
        {
            string apiKey = config.AiApiKey;
            string baseUrl = config.AiBaseUrl;
            string model = config.AiModel;

            if (!string.IsNullOrEmpty(config.ActiveModelId))
            {
                var savedModel = config.SavedModels.FirstOrDefault(m => m.Id == config.ActiveModelId);
                if (savedModel != null)
                {
                    apiKey = savedModel.ApiKey;
                    baseUrl = savedModel.BaseUrl;
                    model = savedModel.ModelName;
                }
            }

            return ChatStreamAsync(messages, apiKey, baseUrl, model);
        }

        public async IAsyncEnumerable<string> ChatStreamAsync(List<ChatMessage> messages, string apiKey, string baseUrl, string model)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                yield return "[设置错误] 请先在设置中配置 API Key";
                yield break;
            }

            // Ensure there's a system message (GLM may require it)
            var hasSystemMessage = messages.Any(m => m.Role == "system");
            if (!hasSystemMessage)
            {
                var defaultSystemMessage = ChatMessage.FromSystem("You are a helpful assistant. Output result directly without unnecessary conversational filler. IMPORTANT: Always answer in Simplified Chinese unless the user explicitly asks for another language.");
                messages.Insert(0, defaultSystemMessage);
                LoggerService.Instance.LogInfo("Added default system message (GLM requirement)", "AiService.ChatStreamAsync");
            }

            // GLM (bigmodel.cn) 需要绕过 SDK 的流式功能，因为 Betalgo SDK v8.7.2 的
            // CreateCompletionAsStream 不使用注入的 HttpClient/Handler，导致 URL 重写不生效
            if (baseUrl.Contains("bigmodel.cn", StringComparison.OrdinalIgnoreCase))
            {
                LoggerService.Instance.LogInfo($"Using native HttpClient for GLM stream: Model={model}", "AiService.ChatStreamAsync");
                
                await foreach (var chunk in NativeStreamAsync(messages, apiKey, baseUrl, model).ConfigureAwait(false))
                {
                    yield return chunk;
                }
                yield break;
            }

            var openAiService = GetOrCreateOpenAiService(apiKey, baseUrl);

            var request = new ChatCompletionCreateRequest
            {
                Messages = messages,
                Model = model,
                Temperature = 0.7f,
                MaxTokens = 4096,
                Stream = true
            };

            LoggerService.Instance.LogInfo($"Starting stream request: Model={model}, BaseUrl={baseUrl}, MessageCount={messages.Count}, MaxTokens=4096", "AiService.ChatStreamAsync");

            await foreach (var completionResult in openAiService.ChatCompletion.CreateCompletionAsStream(request).ConfigureAwait(false))
            {
                if (completionResult.Successful)
                {
                    var choice = completionResult.Choices?.FirstOrDefault();
                    if (choice != null && choice.Message != null && !string.IsNullOrEmpty(choice.Message.Content))
                    {
                        yield return choice.Message.Content;
                    }
                }
                else
                {
                    if (completionResult.Error != null)
                    {
                        string errorMessage = !string.IsNullOrWhiteSpace(completionResult.Error.Message) 
                            ? completionResult.Error.Message 
                            : "未知错误";
                        string errorType = !string.IsNullOrWhiteSpace(completionResult.Error.Type) 
                            ? completionResult.Error.Type 
                            : "Unknown";
                        string errorCode = !string.IsNullOrWhiteSpace(completionResult.Error.Code) 
                            ? completionResult.Error.Code 
                            : "N/A";
                        
                        var errorMsg = $"[AI 错误] {errorMessage} (Type: {errorType}, Code: {errorCode})";
                        LoggerService.Instance.LogError($"Stream Error: {errorMsg}", "AiService.ChatStreamAsync");
                        yield return errorMsg;
                    }
                    else
                    {
                         LoggerService.Instance.LogError("Stream Error: Unknown error (Successful=false, Error=null)", "AiService.ChatStreamAsync");
                         yield return "[AI 错误] 未知错误";
                    }
                }
            }
        }

        /// <summary>
        /// 原生 HttpClient SSE 流式请求 - 专为 GLM 等不兼容 Betalgo SDK 流式功能的 API 设计
        /// </summary>
        private async IAsyncEnumerable<string> NativeStreamAsync(List<ChatMessage> messages, string apiKey, string baseUrl, string model)
        {
            // 构建正确的 URL
            var url = baseUrl.TrimEnd('/') + "/chat/completions";
            LoggerService.Instance.LogInfo($"[GLM Native] Sending to: {url}", "AiService.NativeStreamAsync");

            // 构建请求体
            var requestBody = new
            {
                model = model,
                messages = messages.Select(m => new { role = m.Role, content = m.Content }).ToArray(),
                stream = true,
                temperature = 0.7,
                max_tokens = 4096
            };

            var jsonContent = JsonSerializer.Serialize(requestBody);
            LoggerService.Instance.LogInfo($"[GLM Native] Request body: {jsonContent.Substring(0, Math.Min(200, jsonContent.Length))}...", "AiService.NativeStreamAsync");

            // 发送请求（非迭代器方法，可以使用 try-catch）
            var (response, httpError) = await NativeStreamSendAsync(url, apiKey, jsonContent).ConfigureAwait(false);
            
            if (httpError != null)
            {
                yield return httpError;
                yield break;
            }

            if (response == null)
            {
                yield return "[HTTP 错误] 无响应";
                yield break;
            }

            LoggerService.Instance.LogInfo($"[GLM Native] Response status: {response.StatusCode}", "AiService.NativeStreamAsync");

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                LoggerService.Instance.LogError($"[GLM Native] Error response: {errorBody}", "AiService.NativeStreamAsync");
                yield return $"[AI 错误] HTTP {(int)response.StatusCode}: {errorBody}";
                response.Dispose();
                yield break;
            }

            // 读取 SSE 流
            using (response)
            {
                using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
                using var reader = new StreamReader(stream, Encoding.UTF8);

                while (!reader.EndOfStream)
                {
                    var line = await reader.ReadLineAsync().ConfigureAwait(false);
                    if (string.IsNullOrEmpty(line)) continue;
                    if (!line.StartsWith("data: ")) continue;

                    var data = line.Substring(6); // 去掉 "data: " 前缀
                    if (data == "[DONE]") break;

                    // 解析 JSON（不使用 try-catch，改用 TryParse 模式）
                    var parsed = ParseSseChunk(data);
                    if (parsed.Error != null)
                    {
                        yield return parsed.Error;
                        yield break;
                    }
                    if (parsed.Content != null)
                    {
                        yield return parsed.Content;
                    }
                }
            }
        }

        /// <summary>
        /// 发送原生 HTTP 请求（非迭代器方法，可以安全使用 try-catch）
        /// </summary>
        private async Task<(HttpResponseMessage? Response, string? Error)> NativeStreamSendAsync(string url, string apiKey, string jsonContent)
        {
            try
            {
                var httpClient = _httpClientFactory.CreateClient("NativeAiClient");
                
                var request = new HttpRequestMessage(HttpMethod.Post, url);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
                request.Content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
                return (response, null);
            }
            catch (Exception ex)
            {
                LoggerService.Instance.LogError($"[GLM Native] HTTP error: {ex.Message}", "AiService.NativeStreamSendAsync");
                return (null, $"[HTTP 错误] {ex.Message}");
            }
        }

        /// <summary>
        /// 解析 SSE 数据块（不使用异常处理来避免 yield 限制）
        /// </summary>
        private (string? Content, string? Error) ParseSseChunk(string data)
        {
            try
            {
                using var doc = JsonDocument.Parse(data);
                var root = doc.RootElement;
                
                // 检查是否有错误
                if (root.TryGetProperty("error", out var error))
                {
                    var errorMsg = error.TryGetProperty("message", out var msg) ? msg.GetString() : "未知错误";
                    LoggerService.Instance.LogError($"[GLM Native] API error in stream: {errorMsg}", "AiService.ParseSseChunk");
                    return (null, $"[AI 错误] {errorMsg}");
                }

                if (root.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
                {
                    var firstChoice = choices[0];
                    if (firstChoice.TryGetProperty("delta", out var delta) &&
                        delta.TryGetProperty("content", out var content))
                    {
                        var text = content.GetString();
                        if (!string.IsNullOrEmpty(text))
                        {
                            return (text, null);
                        }
                    }
                }

                return (null, null); // 没有内容也没有错误（可能是角色delta等）
            }
            catch (JsonException ex)
            {
                LoggerService.Instance.LogError($"[GLM Native] JSON parse error: {ex.Message}, Data: {data}", "AiService.ParseSseChunk");
                return (null, null); // 跳过无法解析的行
            }
        }

        public Task<(bool Success, string Message)> TestConnectionAsync(AppConfig config)
        {
            return TestConnectionAsync(config.AiApiKey, config.AiBaseUrl, config.AiModel);
        }

        public async Task<(bool Success, string Message)> TestConnectionAsync(string apiKey, string baseUrl, string model)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
                return (false, "API Key 为空");
            if (string.IsNullOrWhiteSpace(baseUrl))
                return (false, "API 地址为空");
            if (string.IsNullOrWhiteSpace(model))
                return (false, "模型名称为空");

            try
            {
                var openAiService = GetOrCreateOpenAiService(apiKey, baseUrl);

                var request = new ChatCompletionCreateRequest
                {
                    Messages = new List<ChatMessage>
                    {
                        ChatMessage.FromSystem("Test"),
                        ChatMessage.FromUser("Hi")
                    },
                    Model = model,
                    MaxTokens = 5
                };

                var completionResult = await openAiService.ChatCompletion.CreateCompletion(request).ConfigureAwait(false);

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

        private OpenAIService CreateOpenAiService(string apiKey, string baseUrl)
        {
            // 使用缓存版本，避免重复创建
            return GetOrCreateOpenAiService(apiKey, baseUrl);
        }
    }

    /// <summary>
    /// 智谱 AI URL 兼容性处理器
    /// 处理 SDK 自动追加 /v1/ 导致的路径错误
    /// </summary>
    public class ZhipuCompatHandler : DelegatingHandler
    {
        public ZhipuCompatHandler() : base(new HttpClientHandler())
        {
            LoggerService.Instance.LogInfo("ZhipuCompatHandler instance created", "ZhipuCompatHandler");
        }

        public ZhipuCompatHandler(HttpMessageHandler innerHandler) : base(innerHandler)
        {
            LoggerService.Instance.LogInfo("ZhipuCompatHandler instance created with inner handler", "ZhipuCompatHandler");
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LoggerService.Instance.LogInfo($"SendAsync called: URL={request.RequestUri}", "ZhipuCompatHandler");
            
            if (request.RequestUri != null && request.RequestUri.Host.Contains("bigmodel.cn", StringComparison.OrdinalIgnoreCase))
            {
                var uriStr = request.RequestUri.AbsoluteUri;
                
                // 1. 处理 SDK 自动追加 /v1/ 导致路径错误 (如 .../v4/v1/...)
                // 同时处理可能的双斜杠问题 (.../v4//v1/...)
                if (uriStr.Contains("/v4/v1/") || uriStr.Contains("/v4//v1/"))
                {
                    var originalUri = uriStr;
                    
                    // 替换错误的 v1 路径，修正为 v4 直接衔接后续路径
                    var newUriStr = uriStr.Replace("/v4//v1/", "/v4/").Replace("/v4/v1/", "/v4/");
                    
                    request.RequestUri = new Uri(newUriStr);
                    
                    // Log the fix for debugging
                    LoggerService.Instance.LogInfo($"[ZhipuFix] Rewrote URL from {originalUri} to {newUriStr}", "ZhipuCompatHandler");
                }
                else
                {
                     // Log normal requests to verify host match
                     // LoggerService.Instance.LogInfo($"[ZhipuCheck] URL pass-through: {uriStr}", "ZhipuCompatHandler");
                }
            }
            
            // 记录 GLM 请求信息（用于调试）
            if (request.RequestUri != null && request.RequestUri.Host.Contains("bigmodel.cn", StringComparison.OrdinalIgnoreCase))
            {
                LoggerService.Instance.LogInfo($"[GLM Request] URL: {request.RequestUri}, Method: {request.Method}", "ZhipuCompatHandler");
            }
            
            var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
            
            // 记录 GLM 响应状态（流式响应不能读取Body）
            if (request.RequestUri != null && request.RequestUri.Host.Contains("bigmodel.cn", StringComparison.OrdinalIgnoreCase))
            {
                LoggerService.Instance.LogInfo($"[GLM Response] Status: {response.StatusCode}, ContentType: {response.Content.Headers.ContentType}", "ZhipuCompatHandler");
            }
            
            return response;
        }
    }
}

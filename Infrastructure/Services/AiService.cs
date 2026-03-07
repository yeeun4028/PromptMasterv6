using System;
using System.Collections.Concurrent;
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
using PromptMasterv6.Core.Models;
using PromptMasterv6.Core.Interfaces;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;

namespace PromptMasterv6.Infrastructure.Services
{
    public class AiService : IAiService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ISettingsService _settingsService;

        private const int MaxCacheSize = 10;
        private static readonly ConcurrentLruCache<string, OpenAIService> _serviceCache 
            = new ConcurrentLruCache<string, OpenAIService>(MaxCacheSize);

        private static readonly ConcurrentDictionary<string, HttpClient> _httpClientPool = new ConcurrentDictionary<string, HttpClient>();

        public AiService(IHttpClientFactory httpClientFactory, ISettingsService settingsService)
        {
            _httpClientFactory = httpClientFactory;
            _settingsService = settingsService;
        }

        private HttpClient GetPooledHttpClient(bool useProxy)
        {
            string proxyAddress = string.Empty;
            if (useProxy && _settingsService.Config != null)
            {
                proxyAddress = _settingsService.Config.ProxyAddress?.Trim() ?? string.Empty;
            }

            string cacheKey = string.IsNullOrEmpty(proxyAddress) ? "DIRECT" : $"PROXY_{proxyAddress}";

            return _httpClientPool.GetOrAdd(cacheKey, key =>
            {
                LoggerService.Instance.LogInfo($"[HttpClient Pool] Creating new instance for route: {key}", "AiService");

                var handler = new SocketsHttpHandler
                {
                    PooledConnectionLifetime = TimeSpan.FromMinutes(15),
                    UseProxy = !string.IsNullOrEmpty(proxyAddress),
                    Proxy = !string.IsNullOrEmpty(proxyAddress) ? new WebProxy(proxyAddress) { BypassProxyOnLocal = false } : null,
                    MaxConnectionsPerServer = 100
                };

                return new HttpClient(handler, disposeHandler: false)
                {
                    Timeout = TimeSpan.FromMinutes(2)
                };
            });
        }

        private OpenAIService GetOrCreateOpenAiService(string apiKey, string baseUrl, bool useProxy = false)
        {
            string cacheKey = $"{apiKey}|{baseUrl}|{useProxy}";
            return _serviceCache.GetOrAdd(cacheKey, key => CreateOpenAiServiceInternal(apiKey, baseUrl, useProxy));
        }

        private OpenAIService CreateOpenAiServiceInternal(string apiKey, string baseUrl, bool useProxy)
        {
            LoggerService.Instance.LogInfo($"Creating new OpenAiService: BaseUrl={baseUrl}, UseProxy={useProxy}", "AiService.CreateOpenAiServiceInternal");
            
            var options = new OpenAiOptions
            {
                ApiKey = apiKey,
                BaseDomain = baseUrl
            };

            HttpClient httpClient = GetPooledHttpClient(useProxy);

            return new OpenAIService(options, httpClient);
        }

        public Task<string> ChatAsync(string userContent, AppConfig config, string? systemPrompt = null)
        {
            string apiKey = config.AiApiKey;
            string baseUrl = config.AiBaseUrl;
            string model = config.AiModel;
            bool useProxy = false;

            if (!string.IsNullOrEmpty(config.ActiveModelId))
            {
                var savedModel = config.SavedModels.FirstOrDefault(m => m.Id == config.ActiveModelId);
                if (savedModel != null)
                {
                    apiKey = savedModel.ApiKey;
                    baseUrl = savedModel.BaseUrl;
                    model = savedModel.ModelName;
                    useProxy = savedModel.UseProxy;
                }
            }

            return ChatAsync(userContent, apiKey, baseUrl, model, systemPrompt, useProxy);
        }

        public async Task<string> ChatAsync(string userContent, string apiKey, string baseUrl, string model, string? systemPrompt = null, bool useProxy = false)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                return "[设置错误] 请先在设置中配置 API Key";
            }

            var openAiService = GetOrCreateOpenAiService(apiKey, baseUrl, useProxy);

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
                LoggerService.Instance.LogInfo($"[ChatAsync] Sending request to Model={model}, UseProxy={useProxy}", "AiService");
                
                var completionResult = await openAiService.ChatCompletion.CreateCompletion(request).ConfigureAwait(false);

                if (completionResult.Successful)
                {
                    var choice = completionResult.Choices?.FirstOrDefault();
                    if (choice != null && choice.Message != null && !string.IsNullOrEmpty(choice.Message.Content))
                    {
                        LoggerService.Instance.LogInfo($"[ChatAsync] Success! Received {choice.Message.Content.Length} chars.", "AiService");
                        return choice.Message.Content.Trim();
                    }
                    return "[AI 无响应] 返回内容为空";
                }
                else
                {
                    string errorMsg = completionResult.Error == null 
                        ? "[AI 错误] 未知网络错误" 
                        : $"[AI 错误] {completionResult.Error.Message} ({completionResult.Error.Type})";
                    
                    LoggerService.Instance.LogError($"[ChatAsync] API Error: {errorMsg}", "AiService");
                    return errorMsg;
                }
            }
            catch (Exception ex)
            {
                LoggerService.Instance.LogError($"[ChatAsync] Exception: {ex.Message}", "AiService");
                return $"[系统错误] {ex.Message}";
            }
        }

        public async Task<string> InterpretIntentAsync(string systemPrompt, string userText, AppConfig config)
        {
            string apiKey = config.AiApiKey;
            string baseUrl = config.AiBaseUrl;
            string model = config.AiModel;
            bool useProxy = false;

            if (!string.IsNullOrEmpty(config.ActiveModelId))
            {
                var savedModel = config.SavedModels.FirstOrDefault(m => m.Id == config.ActiveModelId);
                if (savedModel != null)
                {
                    apiKey = savedModel.ApiKey;
                    baseUrl = savedModel.BaseUrl;
                    model = savedModel.ModelName;
                    useProxy = savedModel.UseProxy;
                }
            }

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                return "NONE"; // Fail gracefully if no API key is configured
            }

            var openAiService = GetOrCreateOpenAiService(apiKey, baseUrl, useProxy);

            var messages = new List<ChatMessage>
            {
                ChatMessage.FromSystem(systemPrompt),
                ChatMessage.FromUser(userText)
            };

            var request = new ChatCompletionCreateRequest
            {
                Messages = messages,
                Model = model,
                // Low temperature for strict routing matching
                Temperature = 0.1f 
            };

            try
            {
                LoggerService.Instance.LogInfo($"[InterpretIntentAsync] Sending routing request to Model={model}", "AiService");
                var completionResult = await openAiService.ChatCompletion.CreateCompletion(request).ConfigureAwait(false);

                if (completionResult.Successful && completionResult.Choices != null)
                {
                    var choice = completionResult.Choices.FirstOrDefault();
                    if (choice?.Message != null && !string.IsNullOrWhiteSpace(choice.Message.Content))
                    {
                        return choice.Message.Content.Trim();
                    }
                }
                
                LoggerService.Instance.LogWarning("[InterpretIntentAsync] API returned empty or unsuccessful response.", "AiService");
                return "NONE";
            }
            catch (Exception ex)
            {
                LoggerService.Instance.LogError($"[InterpretIntentAsync] Exception: {ex.Message}", "AiService");
                return "NONE";
            }
        }

        public async Task<string> ChatWithImageAsync(byte[] imageBytes, string apiKey, string baseUrl, string model, string? systemPrompt = null, bool useProxy = false)
        {
            if (string.IsNullOrWhiteSpace(apiKey)) return "[设置错误] 请先配置 API Key";
            if (imageBytes == null || imageBytes.Length == 0) return "[输入错误] 图片数据为空";

            var openAiService = GetOrCreateOpenAiService(apiKey, baseUrl, useProxy);

            string finalSystemPrompt = systemPrompt ?? "You are a helpful assistant. Please perform OCR on the provided image.";
            string base64Image = Convert.ToBase64String(imageBytes);
            string imageUrl = $"data:image/jpeg;base64,{base64Image}";

            string ocrPrompt = @"You are a highly precise OCR engine. Your ONLY objective is to extract text from the provided image.

STRICT RULES:
1. Output ONLY the extracted text. Absolutely NO introductory phrases, NO conversational filler (e.g., do not say 'Here is the text' or 'The image contains').
2. Preserve the exact original formatting, line breaks, indentations, and punctuation visible in the image.
3. If there are lists, tables, or code blocks, maintain their structural representation as closely as possible in Markdown format.
4. If no text is found, output exactly nothing (an empty string).
5. Do not explain your output. Do not add markdown code block wrappers (like ```) unless the original text itself is code.

Begin extraction now:";

            var messages = new List<ChatMessage>
            {
                ChatMessage.FromSystem(finalSystemPrompt),
                ChatMessage.FromUser(
                    new List<MessageContent>
                    {
                        MessageContent.ImageUrlContent(imageUrl),
                        MessageContent.TextContent(ocrPrompt)
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
            bool useProxy = false;

            if (!string.IsNullOrEmpty(config.ActiveModelId))
            {
                var savedModel = config.SavedModels.FirstOrDefault(m => m.Id == config.ActiveModelId);
                if (savedModel != null)
                {
                    apiKey = savedModel.ApiKey;
                    baseUrl = savedModel.BaseUrl;
                    model = savedModel.ModelName;
                    useProxy = savedModel.UseProxy;
                }
            }

            return ChatStreamAsync(userContent, apiKey, baseUrl, model, systemPrompt, useProxy);
        }

        public async IAsyncEnumerable<string> ChatStreamAsync(string userContent, string apiKey, string baseUrl, string model, string? systemPrompt = null, bool useProxy = false)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                yield return "[设置错误] 请先在设置中配置 API Key";
                yield break;
            }

            var openAiService = GetOrCreateOpenAiService(apiKey, baseUrl, useProxy);

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
            bool useProxy = false;

            if (!string.IsNullOrEmpty(config.ActiveModelId))
            {
                var savedModel = config.SavedModels.FirstOrDefault(m => m.Id == config.ActiveModelId);
                if (savedModel != null)
                {
                    apiKey = savedModel.ApiKey;
                    baseUrl = savedModel.BaseUrl;
                    model = savedModel.ModelName;
                    useProxy = savedModel.UseProxy;
                }
            }

            return ChatStreamAsync(messages, apiKey, baseUrl, model, useProxy);
        }

        public async IAsyncEnumerable<string> ChatStreamAsync(List<ChatMessage> messages, string apiKey, string baseUrl, string model, bool useProxy = false)
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
                
                await foreach (var chunk in NativeStreamAsync(messages, apiKey, baseUrl, model, useProxy).ConfigureAwait(false))
                {
                    yield return chunk;
                }
                yield break;
            }

            var openAiService = GetOrCreateOpenAiService(apiKey, baseUrl, useProxy);

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
        private async IAsyncEnumerable<string> NativeStreamAsync(List<ChatMessage> messages, string apiKey, string baseUrl, string model, bool useProxy = false)
        {
            // 构建正确的 URL
            var url = baseUrl.TrimEnd('/') + "/chat/completions";
            LoggerService.Instance.LogInfo($"[GLM Native] Sending to: {url}, UseProxy={useProxy}", "AiService.NativeStreamAsync");

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
            var (response, httpError) = await NativeStreamSendAsync(url, apiKey, jsonContent, useProxy).ConfigureAwait(false);
            
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
        private async Task<(HttpResponseMessage? Response, string? Error)> NativeStreamSendAsync(string url, string apiKey, string jsonContent, bool useProxy = false, CancellationToken cancellationToken = default)
        {
            try
            {
                HttpClient httpClient = GetPooledHttpClient(useProxy);
                
                var request = new HttpRequestMessage(HttpMethod.Post, url);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
                request.Content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
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

        public Task<(bool Success, string Message, long? ResponseTimeMs)> TestConnectionAsync(AppConfig config)
        {
            string apiKey = config.AiApiKey;
            string baseUrl = config.AiBaseUrl;
            string model = config.AiModel;
            bool useProxy = false;

            if (!string.IsNullOrEmpty(config.ActiveModelId))
            {
                var savedModel = config.SavedModels.FirstOrDefault(m => m.Id == config.ActiveModelId);
                if (savedModel != null)
                {
                    apiKey = savedModel.ApiKey;
                    baseUrl = savedModel.BaseUrl;
                    model = savedModel.ModelName;
                    useProxy = savedModel.UseProxy;
                }
            }

            return TestConnectionAsync(apiKey, baseUrl, model, useProxy);
        }

        public async Task<(bool Success, string Message, long? ResponseTimeMs)> TestConnectionAsync(string apiKey, string baseUrl, string model, bool useProxy = false)
        {
            LoggerService.Instance.LogInfo($"Testing connection: BaseUrl={baseUrl}, Model={model}, UseProxy={useProxy}", "AiService.TestConnectionAsync");
            
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                LoggerService.Instance.LogError("API Key is empty", "AiService.TestConnectionAsync");
                return (false, "API Key 为空", null);
            }
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                LoggerService.Instance.LogError("Base URL is empty", "AiService.TestConnectionAsync");
                return (false, "API 地址为空", null);
            }
            if (string.IsNullOrWhiteSpace(model))
            {
                LoggerService.Instance.LogError("Model name is empty", "AiService.TestConnectionAsync");
                return (false, "模型名称为空", null);
            }

            try
            {
                var openAiService = GetOrCreateOpenAiService(apiKey, baseUrl, useProxy);
                LoggerService.Instance.LogInfo($"OpenAiService created, sending test request...", "AiService.TestConnectionAsync");

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

                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                var completionResult = await openAiService.ChatCompletion.CreateCompletion(request).ConfigureAwait(false);
                stopwatch.Stop();

                LoggerService.Instance.LogInfo($"Response received: Successful={completionResult.Successful}, Error={completionResult.Error?.Message ?? "null"}", "AiService.TestConnectionAsync");

                if (completionResult.Successful)
                {
                    return (true, "连接成功", stopwatch.ElapsedMilliseconds);
                }
                else
                {
                    var errorMsg = completionResult.Error?.Message ?? "未知错误";
                    LoggerService.Instance.LogError($"API returned error: {errorMsg} (Type: {completionResult.Error?.Type}, Code: {completionResult.Error?.Code})", "AiService.TestConnectionAsync");
                    return (false, $"连接失败: {errorMsg}", null);
                }
            }
            catch (Exception ex)
            {
                LoggerService.Instance.LogException(ex, $"Test connection exception: BaseUrl={baseUrl}, Model={model}", "AiService.TestConnectionAsync");
                return (false, $"连接异常: {ex.Message}", null);
            }
        }

        private OpenAIService CreateOpenAiService(string apiKey, string baseUrl, bool useProxy = false)
        {
            // 使用缓存版本，避免重复创建
            return GetOrCreateOpenAiService(apiKey, baseUrl, useProxy);
        }
    }

    /// <summary>
    /// 智谱 AI URL 兼容性处理器
    /// 处理 SDK 自动追加 /v1/ 导致的路径错误
    /// </summary>
    public class ZhipuCompatHandler : DelegatingHandler
    {
        public ZhipuCompatHandler()
        {
            LoggerService.Instance.LogInfo("ZhipuCompatHandler instance created", "ZhipuCompatHandler");
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

    public class ConcurrentLruCache<TKey, TValue> where TKey : notnull
    {
        private readonly int _capacity;
        private readonly Dictionary<TKey, LinkedListNode<LruItem>> _cache;
        private readonly LinkedList<LruItem> _lruList;
        private readonly object _lock = new object();

        private class LruItem
        {
            public TKey Key { get; }
            public TValue Value { get; }
            public LruItem(TKey k, TValue v) { Key = k; Value = v; }
        }

        public ConcurrentLruCache(int capacity)
        {
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity), "缓存容量必须大于 0");
            _capacity = capacity;
            _cache = new Dictionary<TKey, LinkedListNode<LruItem>>(capacity);
            _lruList = new LinkedList<LruItem>();
        }

        public TValue GetOrAdd(TKey key, Func<TKey, TValue> valueFactory)
        {
            lock (_lock)
            {
                if (_cache.TryGetValue(key, out var node))
                {
                    _lruList.Remove(node);
                    _lruList.AddFirst(node);
                    return node.Value.Value;
                }

                var value = valueFactory(key);
                var newItem = new LruItem(key, value);
                var newNode = new LinkedListNode<LruItem>(newItem);

                if (_cache.Count >= _capacity)
                {
                    var last = _lruList.Last;
                    if (last != null)
                    {
                        _cache.Remove(last.Value.Key);
                        _lruList.RemoveLast();
                        
                        if (last.Value.Value is IDisposable disposable)
                        {
                            disposable.Dispose();
                        }
                    }
                }

                _lruList.AddFirst(newNode);
                _cache[key] = newNode;
                return value;
            }
        }
        
        public void Clear()
        {
            lock (_lock)
            {
                foreach (var item in _lruList)
                {
                    if (item.Value is IDisposable disposable) disposable.Dispose();
                }
                _cache.Clear();
                _lruList.Clear();
            }
        }
    }
}

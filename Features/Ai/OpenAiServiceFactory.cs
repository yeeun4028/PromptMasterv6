using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using OpenAI;
using OpenAI.Managers;
using PromptMasterv6.Infrastructure.Services;

namespace PromptMasterv6.Features.Ai;

public class OpenAiServiceFactory
{
    private readonly LoggerService _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly SettingsService _settingsService;

    private const int MaxCacheSize = 10;
    private static readonly ConcurrentLruCache<string, OpenAIService> _serviceCache 
        = new ConcurrentLruCache<string, OpenAIService>(MaxCacheSize);
    private static readonly ConcurrentDictionary<string, SocketsHttpHandler> _handlerPool = new();

    public OpenAiServiceFactory(
        IHttpClientFactory httpClientFactory, 
        SettingsService settingsService, 
        LoggerService logger)
    {
        _httpClientFactory = httpClientFactory;
        _settingsService = settingsService;
        _logger = logger;
    }

    public OpenAIService GetOrCreateService(string apiKey, string baseUrl, bool useProxy = false)
    {
        string cacheKey = $"{apiKey}|{baseUrl}|{useProxy}";
        return _serviceCache.GetOrAdd(cacheKey, _ => CreateServiceInternal(apiKey, baseUrl, useProxy));
    }

    private OpenAIService CreateServiceInternal(string apiKey, string baseUrl, bool useProxy)
    {
        _logger.LogInfo($"Creating new OpenAiService: BaseUrl={baseUrl}, UseProxy={useProxy}", "OpenAiServiceFactory");
        
        var options = new OpenAiOptions
        {
            ApiKey = apiKey,
            BaseDomain = baseUrl
        };

        HttpClient httpClient = GetPooledHttpClient(useProxy);
        return new OpenAIService(options, httpClient);
    }

    private HttpClient GetPooledHttpClient(bool useProxy)
    {
        string proxyAddress = string.Empty;
        if (useProxy && _settingsService.Config != null)
        {
            proxyAddress = _settingsService.Config.ProxyAddress?.Trim() ?? string.Empty;
        }

        string cacheKey = string.IsNullOrEmpty(proxyAddress) ? "DIRECT" : $"PROXY_{proxyAddress}";

        var handler = _handlerPool.GetOrAdd(cacheKey, key =>
        {
            _logger.LogInfo($"[SocketHandler Pool] Creating new SocketsHttpHandler for route: {key}", "OpenAiServiceFactory");

            return new SocketsHttpHandler
            {
                PooledConnectionLifetime = TimeSpan.FromMinutes(15),
                UseProxy = !string.IsNullOrEmpty(proxyAddress),
                Proxy = !string.IsNullOrEmpty(proxyAddress) 
                    ? new WebProxy(proxyAddress) { BypassProxyOnLocal = false } 
                    : null,
                MaxConnectionsPerServer = 100
            };
        });

        return new HttpClient(handler, disposeHandler: false)
        {
            Timeout = TimeSpan.FromMinutes(2)
        };
    }

    public async Task<(HttpResponseMessage? Response, string? Error)> SendNativeStreamRequestAsync(
        string url, 
        string apiKey, 
        string jsonContent, 
        bool useProxy = false, 
        CancellationToken cancellationToken = default)
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
            _logger.LogError($"[GLM Native] HTTP error: {ex.Message}", "OpenAiServiceFactory");
            return (null, $"[HTTP 错误] {ex.Message}");
        }
    }

    public static (string? Content, string? Error) ParseSseChunk(string data, LoggerService logger)
    {
        try
        {
            using var doc = JsonDocument.Parse(data);
            var root = doc.RootElement;
            
            if (root.TryGetProperty("error", out var error))
            {
                var errorMsg = error.TryGetProperty("message", out var msg) ? msg.GetString() : "未知错误";
                logger.LogError($"[GLM Native] API error in stream: {errorMsg}", "OpenAiServiceFactory");
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

            return (null, null);
        }
        catch (JsonException ex)
        {
            logger.LogError($"[GLM Native] JSON parse error: {ex.Message}, Data: {data}", "OpenAiServiceFactory");
            return (null, null);
        }
    }
}

public class ConcurrentLruCache<TKey, TValue> where TKey : notnull
{
    private readonly int _capacity;
    private readonly Dictionary<TKey, LinkedListNode<LruItem>> _cache;
    private readonly LinkedList<LruItem> _lruList;
    private readonly object _lock = new();

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

using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using PromptMasterv5.Core.Models;

namespace PromptMasterv5.Infrastructure.Services
{
    public class GoogleService
    {
        private readonly HttpClient _httpClient;

        public GoogleService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<string> TranslateAsync(string text, ApiProfile profile)
        {
            // 1. 参数校验
            if (string.IsNullOrWhiteSpace(text)) return "";
            if (string.IsNullOrWhiteSpace(profile.Key1)) return "错误：未配置 Google API Key";

            try
            {
                // 2. 确定 Base URL
                // 优先使用 profile.BaseUrl，如果为空侧使用官方默认地址
                string baseUrl = !string.IsNullOrWhiteSpace(profile.BaseUrl) 
                    ? profile.BaseUrl.TrimEnd('/') 
                    : "https://translation.googleapis.com";

                // 3. 构建请求 URL
                // 拼接 /language/translate/v2，确保路径正确
                string requestUrl = $"{baseUrl}/language/translate/v2";
                
                // 确保协议头存在 (防止用户输入 api.openai.com 而没带 https://)
                if (!requestUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                {
                    requestUrl = "https://" + requestUrl;
                }

                // API Key 通常作为 query parameter 传递
                requestUrl += $"?key={profile.Key1}";

                // 4. 准备请求体
                // source 留空表示自动检测
                var payload = new
                {
                    q = text,
                    target = "zh"
                };
                
                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                // 5. 发送 POST 请求
                var response = await _httpClient.PostAsync(requestUrl, content);
                var responseString = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    // 尝试解析错误信息
                    try
                    {
                        var errorNode = JsonNode.Parse(responseString);
                        var errorMsg = errorNode?["error"]?["message"]?.GetValue<string>();
                        if (!string.IsNullOrWhiteSpace(errorMsg))
                        {
                            return $"Google API 错误: {errorMsg}";
                        }
                    }
                    catch { }
                    return $"Google 请求失败: {response.StatusCode} - {responseString}";
                }

                // 6. 解析响应
                // 格式: { "data": { "translations": [ { "translatedText": "..." } ] } }
                var jsonNode = JsonNode.Parse(responseString);
                var translatedText = jsonNode?["data"]?["translations"]?[0]?["translatedText"]?.GetValue<string>();

                if (string.IsNullOrWhiteSpace(translatedText))
                {
                    return "Google 翻译返回了空结果";
                }
                
                // 解码 HTML 实体 (例如 &#39; -> ')
                return System.Net.WebUtility.HtmlDecode(translatedText);
            }
            catch (Exception ex)
            {
                return $"Google 服务异常: {ex.Message}";
            }
        }
    }
}

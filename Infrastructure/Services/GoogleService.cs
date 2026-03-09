using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using PromptMasterv6.Core.Models;
using PromptMasterv6.Core.Interfaces;

namespace PromptMasterv6.Infrastructure.Services
{
    public class GoogleService : IGoogleService
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
                // 将文本按行拆分，作为数组发送。这能强制 Google 翻译保留每一行的独立性，防止有序列表被合并成一段话。
                var lines = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
                
                var payload = new
                {
                    q = lines,
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
                // 格式: { "data": { "translations": [ { "translatedText": "..." }, { "translatedText": "..." } ] } }
                var jsonNode = JsonNode.Parse(responseString);
                var translationsArray = jsonNode?["data"]?["translations"]?.AsArray();

                if (translationsArray == null || translationsArray.Count == 0)
                {
                    return "Google 翻译返回了空结果";
                }

                StringBuilder sb = new StringBuilder();
                foreach (var item in translationsArray)
                {
                    var translatedRow = item?["translatedText"]?.GetValue<string>();
                    if (translatedRow != null)
                    {
                        // 解码 HTML 实体并追加，保持换行
                        sb.AppendLine(System.Net.WebUtility.HtmlDecode(translatedRow));
                    }
                    else
                    {
                        sb.AppendLine();
                    }
                }
                
                return sb.ToString().TrimEnd();
            }
            catch (Exception ex)
            {
                return $"Google 服务异常: {ex.Message}";
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Web;

namespace PromptMasterv5.Services
{
    public class BaiduService
    {
        private static readonly HttpClient _client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };

        // --- 百度 OCR 相关 ---
        // 注意：OCR 需要先用 AK/SK 获取 AccessToken，然后用 Token 调用识别接口
        // 这里简化处理，每次或缓存 Token。为了稳健，我们实时获取（百度 Token 有效期很长，实际应缓存）

        private class BaiduTokenResponse
        {
            [JsonPropertyName("access_token")] public string AccessToken { get; set; } = "";
        }

        private class BaiduOcrResult
        {
            [JsonPropertyName("words_result")] public List<BaiduOcrWord>? WordsResult { get; set; }
            [JsonPropertyName("error_msg")] public string? ErrorMsg { get; set; }
        }
        private class BaiduOcrWord { [JsonPropertyName("words")] public string Words { get; set; } = ""; }

        // --- 百度翻译 相关 ---
        private class BaiduTransResult
        {
            [JsonPropertyName("trans_result")] public List<BaiduTransItem>? TransResult { get; set; }
            [JsonPropertyName("error_code")] public string? ErrorCode { get; set; }
            [JsonPropertyName("error_msg")] public string? ErrorMsg { get; set; }
        }
        private class BaiduTransItem { [JsonPropertyName("dst")] public string Dst { get; set; } = ""; }

        public async Task<string> OcrAsync(string apiKey, string secretKey, byte[] imageBytes)
        {
            try
            {
                // 1. 获取 Token
                string tokenUrl = $"https://aip.baidubce.com/oauth/2.0/token?grant_type=client_credentials&client_id={apiKey}&client_secret={secretKey}";
                var tokenJson = await _client.GetStringAsync(tokenUrl);
                var tokenObj = JsonSerializer.Deserialize<BaiduTokenResponse>(tokenJson);

                if (string.IsNullOrEmpty(tokenObj?.AccessToken)) return "错误：无法获取百度 AccessToken，请检查 API Key 和 Secret Key。";

                // 2. 调用通用文字识别 (标准版)
                string ocrUrl = $"https://aip.baidubce.com/rest/2.0/ocr/v1/general_basic?access_token={tokenObj.AccessToken}";

                string base64 = Convert.ToBase64String(imageBytes);
                var content = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("image", base64),
                    new KeyValuePair<string, string>("language_type", "CHN_ENG") // 中英混合
                });

                var response = await _client.PostAsync(ocrUrl, content);
                var json = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<BaiduOcrResult>(json);

                if (result == null) return "错误：OCR 返回为空";
                if (!string.IsNullOrEmpty(result.ErrorMsg)) return $"错误：{result.ErrorMsg}";
                if (result.WordsResult == null || result.WordsResult.Count == 0) return "";

                var sb = new StringBuilder();
                foreach (var item in result.WordsResult)
                {
                    sb.AppendLine(item.Words);
                }
                return sb.ToString().Trim();
            }
            catch (Exception ex)
            {
                return $"异常：{ex.Message}";
            }
        }

        public async Task<string> TranslateAsync(string appId, string secretKey, string q)
        {
            if (string.IsNullOrWhiteSpace(q)) return "";
            try
            {
                // 百度翻译通用 API
                string url = "https://api.fanyi.baidu.com/api/trans/vip/translate";
                string salt = DateTime.Now.Ticks.ToString();
                string signStr = appId + q + salt + secretKey;
                string sign = MD5Encrypt(signStr);

                // 自动检测源语言 -> 中文
                // 如果源是中文 -> 英文 (简单逻辑)
                string to = "zh";
                if (IsContainsChinese(q)) to = "en";

                var query = HttpUtility.ParseQueryString(string.Empty);
                query["q"] = q;
                query["from"] = "auto";
                query["to"] = to;
                query["appid"] = appId;
                query["salt"] = salt;
                query["sign"] = sign;

                var response = await _client.GetAsync($"{url}?{query}");
                var json = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<BaiduTransResult>(json);

                if (result == null) return "错误：翻译接口无响应";
                if (!string.IsNullOrEmpty(result.ErrorCode) && result.ErrorCode != "52000")
                    return $"错误代码 {result.ErrorCode}: {result.ErrorMsg}";

                if (result.TransResult == null) return "";

                var sb = new StringBuilder();
                foreach (var item in result.TransResult)
                {
                    sb.AppendLine(item.Dst);
                }
                return sb.ToString().Trim();
            }
            catch (Exception ex)
            {
                return $"异常：{ex.Message}";
            }
        }

        private static string MD5Encrypt(string strText)
        {
            using var md5 = MD5.Create();
            byte[] result = md5.ComputeHash(Encoding.UTF8.GetBytes(strText));
            var sb = new StringBuilder();
            for (int i = 0; i < result.Length; i++) sb.Append(result[i].ToString("x2"));
            return sb.ToString();
        }

        private static bool IsContainsChinese(string str)
        {
            foreach (char c in str) if (c >= 0x4e00 && c <= 0x9fbb) return true;
            return false;
        }
    }
}
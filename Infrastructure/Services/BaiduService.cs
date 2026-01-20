using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using PromptMasterv5.Core.Models;

namespace PromptMasterv5.Services
{
    public class BaiduService
    {
        private string _transAppId = "";
        private string _transSecretKey = "";

        private string _ocrApiKey = "";
        private string _ocrSecretKey = "";
        private string _ocrAccessToken = "";
        private DateTime _ocrTokenExpire = DateTime.MinValue;

        private readonly HttpClient _httpClient;

        public BaiduService(HttpClient httpClient)
        {
            _httpClient = httpClient;
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
        }

        public void Configure(string transAppId, string transSecretKey, string ocrApiKey = "", string ocrSecretKey = "")
        {
            _transAppId = transAppId;
            _transSecretKey = transSecretKey;
            _ocrApiKey = ocrApiKey;
            _ocrSecretKey = ocrSecretKey;

            _ocrAccessToken = "";
        }

        #region 翻译功能 (Translate)

        public Task<string> TranslateAsync(string content, ApiProfile profile, string from = "auto", string to = "zh")
        {
            if (profile == null) return Task.FromResult("错误: 未配置百度翻译 AppID 或 SecretKey");
            _transAppId = (profile.Key1 ?? "").Trim();
            _transSecretKey = (profile.Key2 ?? "").Trim();
            if (!IsAllDigits(_transAppId)) return Task.FromResult("错误: 翻译配置 Key1 必须是 AppID（纯数字）");
            return TranslateAsync(content, from, to);
        }

        public async Task<string> TranslateAsync(string content, string from = "auto", string to = "zh")
        {
            if (string.IsNullOrWhiteSpace(_transAppId) || string.IsNullOrWhiteSpace(_transSecretKey))
                return "错误: 未配置百度翻译 AppID 或 SecretKey";

            if (string.IsNullOrWhiteSpace(content)) return string.Empty;

            try
            {
                string salt = new Random().Next(100000, 999999).ToString();

                string signStr = _transAppId + content + salt + _transSecretKey;
                string sign = ComputeMd5(signStr);

                string url = "https://fanyi-api.baidu.com/api/trans/vip/translate";

                var postData = new List<KeyValuePair<string, string>>
                {
                    new KeyValuePair<string, string>("q", content),
                    new KeyValuePair<string, string>("from", from),
                    new KeyValuePair<string, string>("to", to),
                    new KeyValuePair<string, string>("appid", _transAppId),
                    new KeyValuePair<string, string>("salt", salt),
                    new KeyValuePair<string, string>("sign", sign)
                };

                using (var requestContent = new FormUrlEncodedContent(postData))
                {
                    var response = await _httpClient.PostAsync(url, requestContent);
                    response.EnsureSuccessStatusCode();
                    var jsonResult = await response.Content.ReadAsStringAsync();

                    using (JsonDocument doc = JsonDocument.Parse(jsonResult))
                    {
                        var root = doc.RootElement;

                        if (root.TryGetProperty("error_code", out var errorCodeElement))
                        {
                            string errorCode = errorCodeElement.ToString();
                            if (errorCode != "52000")
                            {
                                string msg = root.TryGetProperty("error_msg", out var errorMsg) ? errorMsg.ToString() : "未知错误";
                                return $"百度翻译错误 ({errorCode}): {msg}";
                            }
                        }

                        if (root.TryGetProperty("trans_result", out var results))
                        {
                            StringBuilder sb = new StringBuilder();
                            foreach (var item in results.EnumerateArray())
                            {
                                if (item.TryGetProperty("dst", out var dst))
                                    sb.AppendLine(dst.ToString());
                            }
                            return sb.ToString().TrimEnd();
                        }
                    }
                }
                return "错误: 未能解析翻译结果";
            }
            catch (Exception ex)
            {
                return $"翻译异常: {ex.Message}";
            }
        }

        #endregion

        #region OCR 功能 (OCR)

        public Task<string> OcrAsync(byte[] imageBytes, ApiProfile profile, string languageType = "CHN_ENG")
        {
            if (profile == null) return Task.FromResult("错误: 未配置百度 OCR API Key 或 Secret Key");
            var nextApiKey = profile.Key1 ?? "";
            var nextSecretKey = profile.Key2 ?? "";
            if (!string.Equals(_ocrApiKey, nextApiKey, StringComparison.Ordinal) ||
                !string.Equals(_ocrSecretKey, nextSecretKey, StringComparison.Ordinal))
            {
                _ocrApiKey = nextApiKey;
                _ocrSecretKey = nextSecretKey;
                _ocrAccessToken = "";
                _ocrTokenExpire = DateTime.MinValue;
            }
            return OcrAsync(imageBytes, languageType);
        }

        public async Task<string> OcrAsync(byte[] imageBytes, string languageType = "CHN_ENG")
        {
            if (string.IsNullOrWhiteSpace(_ocrApiKey) || string.IsNullOrWhiteSpace(_ocrSecretKey))
                return "错误: 未配置百度 OCR API Key 或 Secret Key";

            if (imageBytes == null || imageBytes.Length == 0) return "错误: 图片数据为空";

            try
            {
                string token = await GetOcrAccessTokenAsync();
                if (string.IsNullOrEmpty(token)) return "错误: 无法获取 Access Token";

                string url = $"https://aip.baidubce.com/rest/2.0/ocr/v1/general_basic?access_token={token}";

                string base64Image = Convert.ToBase64String(imageBytes);

                var content = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("image", base64Image),
                    new KeyValuePair<string, string>("language_type", languageType),
                    new KeyValuePair<string, string>("detect_direction", "false"),
                    new KeyValuePair<string, string>("detect_language", "false"),
                    new KeyValuePair<string, string>("paragraph", "false"),
                    new KeyValuePair<string, string>("probability", "false")
                });

                var response = await _httpClient.PostAsync(url, content);
                var jsonResult = await response.Content.ReadAsStringAsync();

                using (JsonDocument doc = JsonDocument.Parse(jsonResult))
                {
                    var root = doc.RootElement;

                    if (root.TryGetProperty("error_code", out var errorCode) && errorCode.GetInt32() != 0)
                    {
                        string msg = root.TryGetProperty("error_msg", out var errorMsg) ? errorMsg.ToString() : "未知错误";
                        return $"OCR 错误 ({errorCode}): {msg}";
                    }

                    if (root.TryGetProperty("words_result", out var wordsResult))
                    {
                        StringBuilder sb = new StringBuilder();
                        foreach (var item in wordsResult.EnumerateArray())
                        {
                            if (item.TryGetProperty("words", out var words))
                            {
                                sb.AppendLine(words.ToString());
                            }
                        }
                        string result = sb.ToString().Trim();
                        return string.IsNullOrWhiteSpace(result) ? "未识别到文字" : result;
                    }
                }
                return "错误: 解析 OCR 结果失败";
            }
            catch (Exception ex)
            {
                return $"OCR 异常: {ex.Message}";
            }
        }

        private async Task<string> GetOcrAccessTokenAsync()
        {
            if (!string.IsNullOrEmpty(_ocrAccessToken) && DateTime.Now < _ocrTokenExpire)
                return _ocrAccessToken;

            try
            {
                string url = $"https://aip.baidubce.com/oauth/2.0/token?grant_type=client_credentials&client_id={_ocrApiKey}&client_secret={_ocrSecretKey}";
                var response = await _httpClient.PostAsync(url, null);
                var json = await response.Content.ReadAsStringAsync();

                using (JsonDocument doc = JsonDocument.Parse(json))
                {
                    var root = doc.RootElement;
                    if (root.TryGetProperty("access_token", out var tokenElement))
                    {
                        _ocrAccessToken = tokenElement.ToString();
                        int expiresIn = root.TryGetProperty("expires_in", out var expireElement) ? expireElement.GetInt32() : 2592000;
                        _ocrTokenExpire = DateTime.Now.AddSeconds(expiresIn - 60);
                        return _ocrAccessToken;
                    }
                }
            }
            catch
            {
            }
            return string.Empty;
        }

        private static string ComputeMd5(string source)
        {
            using (MD5 md5 = MD5.Create())
            {
                byte[] inputBytes = Encoding.UTF8.GetBytes(source);
                byte[] hashBytes = md5.ComputeHash(inputBytes);
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < hashBytes.Length; i++) sb.Append(hashBytes[i].ToString("x2"));
                return sb.ToString();
            }
        }

        private static bool IsAllDigits(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return false;
            for (int i = 0; i < value.Length; i++)
            {
                var c = value[i];
                if (c < '0' || c > '9') return false;
            }
            return true;
        }

        #endregion
    }
}

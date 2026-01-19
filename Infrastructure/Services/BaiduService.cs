using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes; // 必须引用: 用于解析 JSON
using System.Threading.Tasks;

namespace PromptMasterv5.Infrastructure.Services
{
    public class BaiduService
    {
        // 保持 HttpClient 单例，避免重复创建连接导致端口耗尽
        private readonly HttpClient _client;

        public BaiduService()
        {
            _client = new HttpClient();
            // 设置超时，防止网络卡死
            _client.Timeout = TimeSpan.FromSeconds(30);
        }

        /// <summary>
        /// 文本翻译 (修复版)
        /// </summary>
        /// <param name="appId">百度翻译 AppID</param>
        /// <param name="secretKey">百度翻译 密钥</param>
        /// <param name="text">待翻译文本</param>
        /// <param name="from">源语言 (默认 auto)</param>
        /// <param name="to">目标语言 (默认 zh)</param>
        public async Task<string> TranslateAsync(string appId, string secretKey, string text, string from = "auto", string to = "zh")
        {
            // 1. 基础校验
            if (string.IsNullOrWhiteSpace(appId) || string.IsNullOrWhiteSpace(secretKey))
                return "请先在设置中配置百度翻译 AppID 和 密钥";

            if (string.IsNullOrWhiteSpace(text))
                return "翻译内容为空";

            try
            {
                // 2. 生成随机盐 (Salt)
                string salt = DateTime.Now.Ticks.ToString();

                // 3. 生成签名 (Sign)
                // ★★★ 关键点1：签名必须使用【未编码】的原始文本拼接 ★★★
                string rawSignStr = appId + text + salt + secretKey;
                string sign = EncryptString(rawSignStr);

                // 4. 构造请求 URL
                // ★★★ 关键点2：发送请求时，Query 参数中的文本必须经过 URL 编码 ★★★
                // 百度通用翻译 API 地址
                string url = $"https://fanyi-api.baidu.com/api/trans/vip/translate?q={Uri.EscapeDataString(text)}&from={from}&to={to}&appid={appId}&salt={salt}&sign={sign}";

                // 5. 发送请求
                var response = await _client.GetAsync(url);
                var json = await response.Content.ReadAsStringAsync();

                // 6. 解析结果
                var jsonNode = JsonNode.Parse(json);
                if (jsonNode == null) return "翻译接口返回数据为空";

                // 优先检查错误码 (52000 为成功)
                var errorCode = jsonNode["error_code"]?.ToString();
                if (!string.IsNullOrEmpty(errorCode) && errorCode != "52000")
                {
                    var errorMsg = jsonNode["error_msg"]?.ToString() ?? "未知错误";
                    return $"翻译失败 ({errorCode}): {errorMsg}";
                }

                // 提取翻译结果 (处理多段文本)
                if (jsonNode["trans_result"] is JsonArray transArray)
                {
                    var sb = new StringBuilder();
                    foreach (var item in transArray)
                    {
                        var dst = item?["dst"]?.ToString();
                        if (!string.IsNullOrEmpty(dst))
                        {
                            sb.AppendLine(dst);
                        }
                    }
                    return sb.ToString().Trim();
                }

                return $"未解析到翻译结果。原始内容：{json}";
            }
            catch (Exception ex)
            {
                return $"翻译异常：{ex.Message}";
            }
        }

        /// <summary>
        /// 通用文字识别 (OCR) - 标准版
        /// </summary>
        public async Task<string> OcrAsync(string apiKey, string secretKey, byte[] imageBytes)
        {
            if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(secretKey))
                return "请先在设置中配置百度 OCR API Key 和 Secret Key";

            try
            {
                // 1. 获取 Access Token
                // 注意：OCR 的鉴权方式与翻译不同，需要先换取 Token
                string tokenUrl = $"https://aip.baidubce.com/oauth/2.0/token?grant_type=client_credentials&client_id={apiKey}&client_secret={secretKey}";
                var tokenResponse = await _client.PostAsync(tokenUrl, null);
                var tokenJson = await tokenResponse.Content.ReadAsStringAsync();

                var tokenNode = JsonNode.Parse(tokenJson);
                var accessToken = tokenNode?["access_token"]?.ToString();

                if (string.IsNullOrEmpty(accessToken))
                    return "OCR 鉴权失败：无法获取 AccessToken，请检查 Key 是否正确";

                // 2. 调用 OCR 接口
                string ocrUrl = $"https://aip.baidubce.com/rest/2.0/ocr/v1/general_basic?access_token={accessToken}";

                string base64 = Convert.ToBase64String(imageBytes);
                var postData = new List<KeyValuePair<string, string>>
                {
                    new("image", base64),
                    new("language_type", "CHN_ENG") // 识别中英混合
                };

                using var content = new FormUrlEncodedContent(postData);
                var response = await _client.PostAsync(ocrUrl, content);
                var json = await response.Content.ReadAsStringAsync();

                // 3. 解析结果
                var jsonNode = JsonNode.Parse(json);
                if (jsonNode == null) return "OCR 接口返回为空";

                if (jsonNode["error_code"] != null)
                {
                    return $"OCR 失败: {jsonNode["error_msg"]}";
                }

                if (jsonNode["words_result"] is JsonArray wordsArray)
                {
                    var sb = new StringBuilder();
                    foreach (var item in wordsArray)
                    {
                        sb.AppendLine(item?["words"]?.ToString());
                    }
                    return sb.ToString().Trim();
                }

                return "未识别到文字";
            }
            catch (Exception ex)
            {
                return $"OCR 异常: {ex.Message}";
            }
        }

        /// <summary>
        /// MD5 加密工具方法
        /// </summary>
        private static string EncryptString(string str)
        {
            using (var md5 = MD5.Create())
            {
                var byteOld = Encoding.UTF8.GetBytes(str);
                var byteNew = md5.ComputeHash(byteOld);
                var sb = new StringBuilder();
                foreach (var b in byteNew)
                {
                    // ★★★ 关键点3：必须使用 "x2" 格式化为小写十六进制 ★★★
                    // 如果使用 "X2" (大写)，百度 API 会提示签名错误 (54001)
                    sb.Append(b.ToString("x2"));
                }
                return sb.ToString();
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks;
using System.IO;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using PromptMasterv5.Core.Models;

namespace PromptMasterv5.Services
{
    /// <summary>
    /// 百度 AI 服务类
    /// 封装了百度通用翻译 API 和 百度智能云 OCR API
    /// </summary>
    public class BaiduService
    {
        #region 私有字段 & 配置

        // 翻译 API 配置 (api.fanyi.baidu.com)
        private string _transAppId = "";
        private string _transSecretKey = "";

        // OCR API 配置 (cloud.baidu.com)
        // 注意：OCR 的 Key 与翻译的 AppID 是两套完全不同的凭证体系
        private string _ocrApiKey = "";
        private string _ocrSecretKey = "";

        // OCR Access Token 缓存
        private string _ocrAccessToken = "";
        private DateTime _ocrTokenExpire = DateTime.MinValue;

        // 信号量锁：用于确保高并发下只有一个线程去刷新 OCR Token，避免多次无效请求
        private readonly SemaphoreSlim _tokenLock = new SemaphoreSlim(1, 1);

        // HttpClient 实例，建议由外部 IHttpClientFactory 注入单例
        private readonly HttpClient _httpClient;

        #endregion

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="httpClient">注入的 HttpClient 实例</param>
        public BaiduService(HttpClient httpClient)
        {
            // 依赖注入最佳实践：不要在构造函数中修改外部传入的 HttpClient 的 Timeout
            // 如果需要特定超时，建议在 Startup.cs 中配置 HttpClient 或者在请求时使用 CancellationToken
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        }

        /// <summary>
        /// 动态更新配置信息
        /// </summary>
        /// <param name="transAppId">百度翻译 AppID (纯数字)</param>
        /// <param name="transSecretKey">百度翻译 密钥</param>
        /// <param name="ocrApiKey">百度 OCR API Key</param>
        /// <param name="ocrSecretKey">百度 OCR Secret Key</param>
        public void Configure(string transAppId, string transSecretKey, string ocrApiKey = "", string ocrSecretKey = "")
        {
            _transAppId = transAppId;
            _transSecretKey = transSecretKey;
            _ocrApiKey = ocrApiKey;
            _ocrSecretKey = ocrSecretKey;

            // 当配置改变时，旧的 Access Token 可能失效（属于旧账号），因此需要重置
            _ocrAccessToken = "";
            _ocrTokenExpire = DateTime.MinValue;
        }

        #region 翻译功能 (Translate)

        /// <summary>
        /// 执行翻译 (使用 ApiProfile 对象)
        /// </summary>
        public Task<string> TranslateAsync(string content, ApiProfile profile, string from = "auto", string to = "zh")
        {
            if (profile == null) return Task.FromResult("错误: 未配置百度翻译 AppID 或 SecretKey");

            // 防御性编程：去除用户可能误输入的空格
            _transAppId = (profile.Key1 ?? "").Trim();
            _transSecretKey = (profile.Key2 ?? "").Trim();

            // 预检查：AppID 必须是纯数字，如果包含字母说明填错了（可能填成了 API Key）
            if (!IsAllDigits(_transAppId))
                return Task.FromResult("错误: 翻译配置 Key1 必须是 AppID（纯数字），您可能误填了 OCR 的 API Key。");

            return TranslateAsync(content, from, to);
        }

        /// <summary>
        /// 执行翻译 (核心逻辑)
        /// </summary>
        /// <param name="content">待翻译文本</param>
        /// <param name="from">源语言 (默认 auto)</param>
        /// <param name="to">目标语言 (默认 zh)</param>
        /// <returns>翻译结果或错误信息</returns>
        public async Task<string> TranslateAsync(string content, string from = "auto", string to = "zh")
        {
            // 1. 基础校验
            if (string.IsNullOrWhiteSpace(_transAppId) || string.IsNullOrWhiteSpace(_transSecretKey))
                return "错误: 未配置百度翻译 AppID 或 SecretKey";

            if (string.IsNullOrWhiteSpace(content)) return string.Empty;

            try
            {
                // 2. 准备签名参数
                // 随机数，用于签名防重放
                string salt = new Random().Next(100000, 999999).ToString();

                // 签名生成规则：appid + q + salt + 密钥
                // 注意：这里必须使用原始字符串进行 MD5，不要先进行 URL 编码
                string signStr = _transAppId + content + salt + _transSecretKey;
                string sign = ComputeMd5(signStr);

                string url = "https://fanyi-api.baidu.com/api/trans/vip/translate";

                // 3. 构造 POST 请求体
                // 使用 FormUrlEncodedContent 会自动将参数进行 URL 编码，避免手动编码导致的错误
                // 同时也避免了 GET 请求长度受限 (2KB) 的问题
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
                    // 4. 发送请求
                    var response = await _httpClient.PostAsync(url, requestContent);
                    response.EnsureSuccessStatusCode(); // 如果 HTTP 状态码不是 200，直接抛异常
                    var jsonResult = await response.Content.ReadAsStringAsync();

                    // 5. 解析 JSON 响应
                    using (JsonDocument doc = JsonDocument.Parse(jsonResult))
                    {
                        var root = doc.RootElement;

                        // 检查是否存在错误码
                        if (root.TryGetProperty("error_code", out var errorCodeElement))
                        {
                            string errorCode = errorCodeElement.ToString();
                            // 百度翻译 API 中，52000 代表成功，其他均为错误
                            if (errorCode != "52000")
                            {
                                string msg = root.TryGetProperty("error_msg", out var errorMsg) ? errorMsg.ToString() : "未知错误";
                                return $"百度翻译错误 ({errorCode}): {msg}";
                            }
                        }

                        // 提取翻译结果
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
                return "错误: 未能解析翻译结果，JSON 结构不匹配";
            }
            catch (Exception ex)
            {
                return $"翻译请求异常: {ex.Message}";
            }
        }

        #endregion

        #region OCR 功能 (OCR)

        /// <summary>
        /// 执行 OCR 识别
        /// </summary>
        /// <param name="imageBytes">图片二进制数据</param>
        /// <param name="profile">配置信息</param>
        /// <param name="languageType">语言类型 (默认 CHN_ENG)</param>
        public Task<string> OcrAsync(byte[] imageBytes, ApiProfile profile, string languageType = "CHN_ENG")
        {
            if (profile == null) return Task.FromResult("错误: 未配置百度 OCR API Key 或 Secret Key");

            var nextApiKey = (profile.Key1 ?? "").Trim();
            var nextSecretKey = (profile.Key2 ?? "").Trim();

            // 检查配置是否变更，如果变更则需要强制重新获取 Token
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

        /// <summary>
        /// OCR 核心逻辑
        /// </summary>
        public async Task<string> OcrAsync(byte[] imageBytes, string languageType = "CHN_ENG")
        {
            if (string.IsNullOrWhiteSpace(_ocrApiKey) || string.IsNullOrWhiteSpace(_ocrSecretKey))
                return "错误: 未配置百度 OCR API Key 或 Secret Key";

            if (imageBytes == null || imageBytes.Length == 0) return "错误: 图片数据为空";

            // AntiGravity: Optimize image size and dimensions for Baidu OCR
            // Baidu Limit: Base64 < 4MB, Side 15px-4096px
            try
            {
                imageBytes = await OptimizeImageAsync(imageBytes);
            }
            catch (Exception ex)
            {
                return $"图片处理失败: {ex.Message}";
            }

            try
            {
                // 1. 获取 Access Token (包含缓存和并发控制逻辑)
                string token = await GetOcrAccessTokenAsync();
                if (string.IsNullOrEmpty(token)) return "错误: 无法获取 Access Token，请检查 Key 配置是否正确。";

                // 2. 构造请求 URL (通用文字识别-标准版)
                // 如果您购买的是高精度版，请将 general_basic 改为 accurate_basic
                string url = $"https://aip.baidubce.com/rest/2.0/ocr/v1/general_basic?access_token={token}";

                // 3. 图片转 Base64
                // 注意：百度限制 Base64 后的大小，通常不要超过 4M
                string base64Image = Convert.ToBase64String(imageBytes);

                // 4. 构造表单数据
                // 百度 OCR 推荐 content-type 为 application/x-www-form-urlencoded
                var content = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("image", base64Image),
                    new KeyValuePair<string, string>("language_type", languageType),
                    new KeyValuePair<string, string>("detect_direction", "false"), // 是否检测朝向
                    new KeyValuePair<string, string>("paragraph", "true"),         // YES: 启用分段，以保留有序列表等格式
                    new KeyValuePair<string, string>("probability", "false")       // 是否返回置信度
                });

                // 5. 发送请求
                var response = await _httpClient.PostAsync(url, content);
                var jsonResult = await response.Content.ReadAsStringAsync();

                // 6. 解析结果
                using (JsonDocument doc = JsonDocument.Parse(jsonResult))
                {
                    var root = doc.RootElement;

                    // OCR 接口错误时，error_code 通常不为 0 (或者不存在)
                    if (root.TryGetProperty("error_code", out var errorCode) && errorCode.GetInt32() != 0)
                    {
                        string msg = root.TryGetProperty("error_msg", out var errorMsg) ? errorMsg.ToString() : "未知错误";
                        return $"OCR 接口返回错误 ({errorCode}): {msg}";
                    }

                    // 1. 先提取所有的文字行到列表中，方便后续按索引查表
                    List<string> allWordsLines = new List<string>();
                    if (root.TryGetProperty("words_result", out var wordsResult))
                    {
                        foreach (var item in wordsResult.EnumerateArray())
                        {
                            if (item.TryGetProperty("words", out var words))
                                allWordsLines.Add(words.ToString());
                        }
                    }

                    if (allWordsLines.Count == 0) return "未识别到文字";

                    // 2. 优先提取 paragraphs_result，根据其中的索引重新组合段落
                    if (root.TryGetProperty("paragraphs_result", out var paragraphsResult))
                    {
                        StringBuilder sb = new StringBuilder();
                        foreach (var paragraph in paragraphsResult.EnumerateArray())
                        {
                            // 百度返回的是该段落对应的文字行在 words_result 中的索引数组
                            if (paragraph.TryGetProperty("words_result_idx", out var idxArray))
                            {
                                foreach (var idxElement in idxArray.EnumerateArray())
                                {
                                    int idx = idxElement.GetInt32();
                                    if (idx >= 0 && idx < allWordsLines.Count)
                                    {
                                        sb.AppendLine(allWordsLines[idx]);
                                    }
                                }
                                // 段落结束，增加一个额外的换行符，确保 Google 翻译识别为独立段落/列表项
                                sb.AppendLine(); 
                            }
                        }
                        string result = sb.ToString().Trim();
                        return string.IsNullOrWhiteSpace(result) ? "未识别到文字" : result;
                    }
                    else
                    {
                        // 降级：如果没有分段信息，直接按行拼接
                        return string.Join(Environment.NewLine, allWordsLines).Trim();
                    }
                }
            }
            catch (Exception ex)
            {
                return $"OCR 请求异常: {ex.Message}";
            }
        }

        /// <summary>
        /// 获取或刷新 OCR Access Token (线程安全)
        /// </summary>
        private async Task<string> GetOcrAccessTokenAsync()
        {
            // 1. 第一重检查：如果 Token 存在且未过期，直接返回 (无锁，高性能)
            // 提前 60 秒认为过期，留出缓冲时间
            if (!string.IsNullOrEmpty(_ocrAccessToken) && DateTime.Now < _ocrTokenExpire)
                return _ocrAccessToken;

            // 2. 加锁：防止多线程同时发起 Access Token 请求 (如同时识别 5 张图)
            await _tokenLock.WaitAsync();
            try
            {
                // 3. 第二重检查：等待锁的过程中，可能已有其他线程刷新了 Token
                if (!string.IsNullOrEmpty(_ocrAccessToken) && DateTime.Now < _ocrTokenExpire)
                    return _ocrAccessToken;

                string url = $"https://aip.baidubce.com/oauth/2.0/token?grant_type=client_credentials&client_id={_ocrApiKey}&client_secret={_ocrSecretKey}";

                var response = await _httpClient.PostAsync(url, null);
                var json = await response.Content.ReadAsStringAsync();

                using (JsonDocument doc = JsonDocument.Parse(json))
                {
                    var root = doc.RootElement;

                    if (root.TryGetProperty("access_token", out var tokenElement))
                    {
                        _ocrAccessToken = tokenElement.ToString();

                        // 获取过期时间，默认 30 天 (2592000秒)
                        int expiresIn = root.TryGetProperty("expires_in", out var expireElement) ? expireElement.GetInt32() : 2592000;

                        // 设置过期时间 (当前时间 + 有效期 - 缓冲时间)
                        _ocrTokenExpire = DateTime.Now.AddSeconds(expiresIn - 60);

                        return _ocrAccessToken;
                    }

                    // 如果获取失败，可以记录日志。这里简单处理为清空 Token
                    if (root.TryGetProperty("error", out var error))
                    {
                        // 常见错误：invalid_client (Key 填错了)
                        // System.Diagnostics.Debug.WriteLine($"Token Error: {error}");
                        _ocrAccessToken = "";
                    }
                }
            }
            catch
            {
                // 网络异常等
                _ocrAccessToken = "";
            }
            finally
            {
                // 确保必须释放锁
                _tokenLock.Release();
            }

            return string.Empty;
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 计算字符串的 MD5 哈希值 (32位小写)
        /// </summary>
        private static string ComputeMd5(string source)
        {
            using (MD5 md5 = MD5.Create())
            {
                // 百度要求 UTF-8 编码
                byte[] inputBytes = Encoding.UTF8.GetBytes(source);
                byte[] hashBytes = md5.ComputeHash(inputBytes);

                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < hashBytes.Length; i++)
                    sb.Append(hashBytes[i].ToString("x2")); // "x2" 表示转为小写16进制

                return sb.ToString();
            }
        }

        /// <summary>
        /// 检查字符串是否全为数字 (用于校验 AppID)
        /// </summary>
        private static bool IsAllDigits(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return false;
            foreach (var c in value)
            {
                if (!char.IsDigit(c)) return false;
            }
            return true;
        }

        #endregion


        #region 图片优化逻辑 (Image Optimization)

        /// <summary>
        /// 优化图片尺寸和大小以符合百度 API 限制
        /// 限制：长宽最大 4096px，最短 15px，Base64 后大小 < 4MB (原图建议 < 3MB)
        /// </summary>
        /// <summary>
        /// 异步优化图片尺寸和大小以符合百度 API 限制
        /// 性能优化：将图片处理放到后台线程，避免阻塞调用线程
        /// </summary>
        private Task<byte[]> OptimizeImageAsync(byte[] originalBytes)
        {
            return Task.Run(() => OptimizeImageCore(originalBytes));
        }

        /// <summary>
        /// 图片优化核心逻辑（同步执行，由异步方法包装）
        /// </summary>
        private byte[] OptimizeImageCore(byte[] originalBytes)
        {
            try
            {
                using (var ms = new MemoryStream(originalBytes))
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.StreamSource = ms;
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    bitmap.Freeze();

                    double width = bitmap.PixelWidth;
                    double height = bitmap.PixelHeight;

                    // 1. 检查尺寸 (Max 4096px)
                    // 留出安全余量，限制在 3500px 以内
                    double maxDimension = 3500; 
                    double scale = 1.0;

                    if (width > maxDimension || height > maxDimension)
                    {
                        scale = Math.Min(maxDimension / width, maxDimension / height);
                    }

                    // 2. 如果尺寸过大，或者文件提及过大 (>2MB)，则重绘
                    if (scale < 1.0 || originalBytes.Length > 2 * 1024 * 1024)
                    {
                        BitmapSource finalBitmap = bitmap;
                        
                        if (scale < 1.0)
                        {
                            finalBitmap = new TransformedBitmap(bitmap, new ScaleTransform(scale, scale));
                        }

                        // 3. 压缩为 JPEG
                        using (var outStream = new MemoryStream())
                        {
                            var encoder = new JpegBitmapEncoder();
                            encoder.QualityLevel = 85; // 85% 质量通常足够清晰且体积小
                            encoder.Frames.Add(BitmapFrame.Create(finalBitmap));
                            encoder.Save(outStream);
                            
                            var resultBytes = outStream.ToArray();

                            // 4. 二次检查大小 (Max 4MB base64 ~= 3MB binary)
                            // 如果仍然 > 3MB，降低质量
                            if (resultBytes.Length > 3 * 1024 * 1024)
                            {
                                outStream.SetLength(0);
                                encoder.QualityLevel = 60; // 降低质量
                                encoder.Save(outStream);
                                resultBytes = outStream.ToArray();
                            }
                            
                            LoggerService.Instance.LogInfo($"Image Optimized: Original {originalBytes.Length/1024}KB -> Optimized {resultBytes.Length/1024}KB (Scale: {scale:F2})", "BaiduService.OptimizeImage");
                            return resultBytes;
                        }
                    }
                    
                    return originalBytes;
                }
            }
            catch (Exception ex)
            {
                LoggerService.Instance.LogException(ex, "Image Optimization Failed", "BaiduService");
                // 降级策略：如果处理失败，返回原图尝试发送
                return originalBytes;
            }
        }

        #endregion
    }
}
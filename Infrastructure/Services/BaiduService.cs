using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using PromptMasterv6.Core.Interfaces;

namespace PromptMasterv6.Infrastructure.Services
{
    public class BaiduService : IBaiduService
    {
        private readonly HttpClient _httpClient;
        private readonly SemaphoreSlim _tokenLock = new SemaphoreSlim(1, 1);
        private readonly ConcurrentDictionary<string, (string token, DateTime expire)> _tokenCache = new();

        public BaiduService(HttpClient httpClient)
        {
            var handler = new HttpClientHandler
            {
                UseProxy = false,
                Proxy = null
            };
            
            _httpClient = new HttpClient(handler);
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
        }

        #region 翻译功能

        public Task<string> TranslateAsync(string content, ApiProfile profile, string from = "auto", string to = "zh", CancellationToken cancellationToken = default)
        {
            if (profile == null) return Task.FromResult("错误: 未配置百度翻译 AppID 或 SecretKey");

            var appId = (profile.Key1 ?? "").Trim();
            var secretKey = (profile.Key2 ?? "").Trim();

            if (!IsAllDigits(appId))
                return Task.FromResult("错误: 翻译配置 Key1 必须是 AppID（纯数字），您可能误填了 OCR 的 API Key。");

            return TranslateCoreAsync(content, appId, secretKey, from, to, cancellationToken);
        }

        private async Task<string> TranslateCoreAsync(string content, string appId, string secretKey, string from, string to, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(appId) || string.IsNullOrWhiteSpace(secretKey))
                return "错误: 未配置百度翻译 AppID 或 SecretKey";

            if (string.IsNullOrWhiteSpace(content)) return string.Empty;

            try
            {
                string salt = new Random().Next(100000, 999999).ToString();
                string signStr = appId + content + salt + secretKey;
                string sign = ComputeMd5(signStr);

                string url = "https://fanyi-api.baidu.com/api/trans/vip/translate";

                var postData = new List<KeyValuePair<string, string>>
                {
                    new KeyValuePair<string, string>("q", content),
                    new KeyValuePair<string, string>("from", from),
                    new KeyValuePair<string, string>("to", to),
                    new KeyValuePair<string, string>("appid", appId),
                    new KeyValuePair<string, string>("salt", salt),
                    new KeyValuePair<string, string>("sign", sign)
                };

                using (var requestContent = new FormUrlEncodedContent(postData))
                {
                    var response = await _httpClient.PostAsync(url, requestContent, cancellationToken).ConfigureAwait(false);
                    response.EnsureSuccessStatusCode();
                    var jsonResult = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

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
                return "错误: 未能解析翻译结果，JSON 结构不匹配";
            }
            catch (OperationCanceledException)
            {
                return "翻译请求已取消";
            }
            catch (Exception ex)
            {
                return $"翻译请求异常: {ex.Message}";
            }
        }

        #endregion

        #region OCR 功能

        public Task<string> OcrAsync(byte[] imageBytes, ApiProfile profile, string languageType = "CHN_ENG", CancellationToken cancellationToken = default)
        {
            if (profile == null) return Task.FromResult("错误: 未配置百度 OCR API Key 或 Secret Key");

            var apiKey = (profile.Key1 ?? "").Trim();
            var secretKey = (profile.Key2 ?? "").Trim();

            return OcrCoreAsync(imageBytes, apiKey, secretKey, languageType, cancellationToken);
        }

        private async Task<string> OcrCoreAsync(byte[] imageBytes, string apiKey, string secretKey, string languageType, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(secretKey))
                return "错误: 未配置百度 OCR API Key 或 Secret Key";

            if (imageBytes == null || imageBytes.Length == 0) return "错误: 图片数据为空";

            try
            {
                imageBytes = await OptimizeImageAsync(imageBytes, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return "图片处理已取消";
            }
            catch (Exception ex)
            {
                return $"图片处理失败: {ex.Message}";
            }

            try
            {
                string token = await GetOcrAccessTokenAsync(apiKey, secretKey, cancellationToken).ConfigureAwait(false);
                if (string.IsNullOrEmpty(token)) return "错误: 无法获取 Access Token，请检查 Key 配置是否正确。";

                string url = $"https://aip.baidubce.com/rest/2.0/ocr/v1/general_basic?access_token={token}";
                string base64Image = Convert.ToBase64String(imageBytes);

                var content = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("image", base64Image),
                    new KeyValuePair<string, string>("language_type", languageType),
                    new KeyValuePair<string, string>("detect_direction", "false"),
                    new KeyValuePair<string, string>("paragraph", "true"),
                    new KeyValuePair<string, string>("probability", "false")
                });

                var response = await _httpClient.PostAsync(url, content, cancellationToken).ConfigureAwait(false);
                var jsonResult = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

                using (JsonDocument doc = JsonDocument.Parse(jsonResult))
                {
                    var root = doc.RootElement;

                    if (root.TryGetProperty("error_code", out var errorCode) && errorCode.GetInt32() != 0)
                    {
                        string msg = root.TryGetProperty("error_msg", out var errorMsg) ? errorMsg.ToString() : "未知错误";
                        return $"OCR 接口返回错误 ({errorCode}): {msg}";
                    }

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

                    if (root.TryGetProperty("paragraphs_result", out var paragraphsResult))
                    {
                        StringBuilder sb = new StringBuilder();
                        foreach (var paragraph in paragraphsResult.EnumerateArray())
                        {
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
                                sb.AppendLine();
                            }
                        }
                        string result = sb.ToString().Trim();
                        return string.IsNullOrWhiteSpace(result) ? "未识别到文字" : result;
                    }
                    else
                    {
                        return string.Join(Environment.NewLine, allWordsLines).Trim();
                    }
                }
            }
            catch (OperationCanceledException)
            {
                return "OCR 请求已取消";
            }
            catch (Exception ex)
            {
                return $"OCR 请求异常: {ex.Message}";
            }
        }

        private async Task<string> GetOcrAccessTokenAsync(string apiKey, string secretKey, CancellationToken cancellationToken)
        {
            string cacheKey = apiKey;
            
            if (_tokenCache.TryGetValue(cacheKey, out var cached) && DateTime.Now < cached.expire)
                return cached.token;

            await _tokenLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (_tokenCache.TryGetValue(cacheKey, out cached) && DateTime.Now < cached.expire)
                    return cached.token;

                string url = $"https://aip.baidubce.com/oauth/2.0/token?grant_type=client_credentials&client_id={apiKey}&client_secret={secretKey}";

                var response = await _httpClient.PostAsync(url, null, cancellationToken).ConfigureAwait(false);
                var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

                using (JsonDocument doc = JsonDocument.Parse(json))
                {
                    var root = doc.RootElement;

                    if (root.TryGetProperty("access_token", out var tokenElement))
                    {
                        string token = tokenElement.ToString();
                        int expiresIn = root.TryGetProperty("expires_in", out var expireElement) ? expireElement.GetInt32() : 2592000;
                        DateTime expireTime = DateTime.Now.AddSeconds(expiresIn - 60);

                        _tokenCache[cacheKey] = (token, expireTime);
                        return token;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
            }
            finally
            {
                _tokenLock.Release();
            }

            return string.Empty;
        }

        #endregion

        #region 辅助方法

        private static string ComputeMd5(string source)
        {
            using (MD5 md5 = MD5.Create())
            {
                byte[] inputBytes = Encoding.UTF8.GetBytes(source);
                byte[] hashBytes = md5.ComputeHash(inputBytes);

                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < hashBytes.Length; i++)
                    sb.Append(hashBytes[i].ToString("x2"));

                return sb.ToString();
            }
        }

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

        #region 图片优化逻辑

        private Task<byte[]> OptimizeImageAsync(byte[] originalBytes, CancellationToken cancellationToken)
        {
            return Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return OptimizeImageCore(originalBytes);
            }, cancellationToken);
        }

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
                    double maxDimension = 3500;
                    double scale = 1.0;

                    if (width > maxDimension || height > maxDimension)
                    {
                        scale = Math.Min(maxDimension / width, maxDimension / height);
                    }

                    if (scale < 1.0 || originalBytes.Length > 2 * 1024 * 1024)
                    {
                        BitmapSource finalBitmap = bitmap;

                        if (scale < 1.0)
                        {
                            var transformed = new TransformedBitmap(bitmap, new ScaleTransform(scale, scale));
                            transformed.Freeze();
                            finalBitmap = transformed;
                        }

                        using (var outStream = new MemoryStream())
                        {
                            var encoder = new JpegBitmapEncoder();
                            encoder.QualityLevel = 85;
                            encoder.Frames.Add(BitmapFrame.Create(finalBitmap));
                            encoder.Save(outStream);

                            var resultBytes = outStream.ToArray();

                            if (resultBytes.Length > 3 * 1024 * 1024)
                            {
                                outStream.SetLength(0);
                                encoder.QualityLevel = 60;
                                encoder.Save(outStream);
                                resultBytes = outStream.ToArray();
                            }

                            LoggerService.Instance.LogInfo($"Image Optimized: Original {originalBytes.Length / 1024}KB -> Optimized {resultBytes.Length / 1024}KB (Scale: {scale:F2})", "BaiduService.OptimizeImage");
                            return resultBytes;
                        }
                    }

                    return originalBytes;
                }
            }
            catch (Exception ex)
            {
                LoggerService.Instance.LogException(ex, "Image Optimization Failed", "BaiduService");
                return originalBytes;
            }
        }

        #endregion
    }
}

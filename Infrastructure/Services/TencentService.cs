using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using PromptMasterv5.Core.Models;
using PromptMasterv5.Infrastructure.Services;

namespace PromptMasterv5.Services
{
    public class TencentService
    {
        private readonly HttpClient _httpClient;

        public TencentService(HttpClient httpClient)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        }

        #region Translation

        public Task<string> TranslateAsync(string content, ApiProfile profile, string from = "auto", string to = "zh")
        {
            if (profile == null) return Task.FromResult("Error: Missing Tencent Configuration");
            
            var secretId = (profile.Key1 ?? "").Trim();
            var secretKey = (profile.Key2 ?? "").Trim();

            return TranslateCoreAsync(content, secretId, secretKey, from, to);
        }

        private async Task<string> TranslateCoreAsync(string content, string secretId, string secretKey, string source, string target)
        {
            if (string.IsNullOrWhiteSpace(secretId) || string.IsNullOrWhiteSpace(secretKey))
                return "Error: Missing SecretId or SecretKey";

            try
            {
                string endpoint = "tmt.tencentcloudapi.com";
                string region = "ap-guangzhou"; 
                string action = "TextTranslate";
                string version = "2018-03-21";
                string service = "tmt";

                var payload = new Dictionary<string, object>
                {
                    { "SourceText", content },
                    { "Source", source },
                    { "Target", target },
                    { "ProjectId", 0 }
                };
                string requestPayload = JsonSerializer.Serialize(payload);

                return await SendV3RequestAsync(service, endpoint, region, action, version, requestPayload, secretId, secretKey, root => 
                {
                     if (root.TryGetProperty("Response", out var resp))
                     {
                         if (resp.TryGetProperty("Error", out var error))
                         {
                             return $"Tencent Error: {error.GetProperty("Message").GetString()}";
                         }
                         if (resp.TryGetProperty("TargetText", out var text))
                         {
                             return text.GetString() ?? string.Empty;
                         }
                     }
                     return "Error: Invalid Response Structure";
                });
            }
            catch (Exception ex)
            {
                LoggerService.Instance.LogException(ex, "Tencent Translate Failed", "TencentService");
                return $"Error: {ex.Message}";
            }
        }

        #endregion

        #region OCR

        public Task<string> OcrAsync(byte[] imageBytes, ApiProfile profile)
        {
            if (profile == null) return Task.FromResult("Error: Missing Tencent Configuration");

            var secretId = (profile.Key1 ?? "").Trim();
            var secretKey = (profile.Key2 ?? "").Trim();

            return OcrCoreAsync(imageBytes, secretId, secretKey);
        }

        private async Task<string> OcrCoreAsync(byte[] imageBytes, string secretId, string secretKey)
        {
            if (string.IsNullOrWhiteSpace(secretId) || string.IsNullOrWhiteSpace(secretKey))
                return "Error: Missing SecretId or SecretKey";
            
            try
            {
                imageBytes = await OptimizeImageAsync(imageBytes);
                
                string endpoint = "ocr.tencentcloudapi.com";
                string region = "ap-guangzhou"; 
                string action = "GeneralBasicOCR";
                string version = "2018-11-19";
                string service = "ocr";

                string base64Image = Convert.ToBase64String(imageBytes);
                
                var payload = new Dictionary<string, object>
                {
                    { "ImageBase64", base64Image }
                };
                string requestPayload = JsonSerializer.Serialize(payload);

                return await SendV3RequestAsync(service, endpoint, region, action, version, requestPayload, secretId, secretKey, root =>
                {
                    if (root.TryGetProperty("Response", out var resp))
                    {
                        if (resp.TryGetProperty("Error", out var error))
                        {
                            return $"Tencent Error: {error.GetProperty("Message").GetString()}";
                        }
                        if (resp.TryGetProperty("TextDetections", out var detections))
                        {
                            StringBuilder sb = new StringBuilder();
                            foreach (var item in detections.EnumerateArray())
                            {
                                if (item.TryGetProperty("DetectedText", out var text))
                                {
                                    sb.AppendLine(text.GetString());
                                }
                            }
                            return sb.ToString().Trim();
                        }
                    }
                     return "Error: Invalid Response Structure";
                });
            }
            catch (Exception ex)
            {
                LoggerService.Instance.LogException(ex, "Tencent OCR Failed", "TencentService");
                return $"Error: {ex.Message}";
            }
        }

        #endregion

        #region V3 Signature Helper

        private async Task<string> SendV3RequestAsync(string service, string host, string region, string action, string version, string requestPayload, string secretId, string secretKey, Func<JsonElement, string> responseParser)
        {
            DateTime now = DateTime.UtcNow;
            long timestamp = new DateTimeOffset(now).ToUnixTimeSeconds();
            string date = now.ToString("yyyy-MM-dd");

            string httpRequestMethod = "POST";
            string canonicalUri = "/";
            string canonicalQueryString = "";
            string canonicalHeaders = $"content-type:application/json; charset=utf-8\nhost:{host}\n";
            string signedHeaders = "content-type;host";
            string hashedRequestPayload = SHA256Hex(requestPayload);
            string canonicalRequest = $"{httpRequestMethod}\n{canonicalUri}\n{canonicalQueryString}\n{canonicalHeaders}\n{signedHeaders}\n{hashedRequestPayload}";

            string algorithm = "TC3-HMAC-SHA256";
            string credentialScope = $"{date}/{service}/tc3_request";
            string hashedCanonicalRequest = SHA256Hex(canonicalRequest);
            string stringToSign = $"{algorithm}\n{timestamp}\n{credentialScope}\n{hashedCanonicalRequest}";

            byte[] kDate = HmacSHA256(Encoding.UTF8.GetBytes("TC3" + secretKey), date);
            byte[] kService = HmacSHA256(kDate, service);
            byte[] kSigning = HmacSHA256(kService, "tc3_request");
            string signature = BitConverter.ToString(HmacSHA256(kSigning, stringToSign)).Replace("-", "").ToLower();

            string authorization = $"{algorithm} Credential={secretId}/{credentialScope}, SignedHeaders={signedHeaders}, Signature={signature}";

            try 
            {
                using (var request = new HttpRequestMessage(HttpMethod.Post, $"https://{host}"))
                {
                    request.Headers.TryAddWithoutValidation("Authorization", authorization);
                    request.Headers.TryAddWithoutValidation("Content-Type", "application/json; charset=utf-8");
                    request.Headers.TryAddWithoutValidation("Host", host);
                    request.Headers.TryAddWithoutValidation("X-TC-Action", action);
                    request.Headers.TryAddWithoutValidation("X-TC-Version", version);
                    request.Headers.TryAddWithoutValidation("X-TC-Timestamp", timestamp.ToString());
                    request.Headers.TryAddWithoutValidation("X-TC-Region", region);

                    request.Content = new StringContent(requestPayload, Encoding.UTF8, "application/json");

                    var response = await _httpClient.SendAsync(request);
                    string jsonResponse = await response.Content.ReadAsStringAsync();

                    using (JsonDocument doc = JsonDocument.Parse(jsonResponse))
                    {
                        return responseParser(doc.RootElement);
                    }
                }
            }
            catch (Exception)
            {
                throw;
            }
        }

        private static string SHA256Hex(string s)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(s));
                return BitConverter.ToString(bytes).Replace("-", "").ToLower();
            }
        }

        private static byte[] HmacSHA256(byte[] key, string msg)
        {
            using (HMACSHA256 hmac = new HMACSHA256(key))
            {
                return hmac.ComputeHash(Encoding.UTF8.GetBytes(msg));
            }
        }

        #endregion

        #region 图片优化逻辑

        private Task<byte[]> OptimizeImageAsync(byte[] originalBytes)
        {
            return Task.Run(() => OptimizeImageCore(originalBytes));
        }

        private byte[] OptimizeImageCore(byte[] originalBytes)
        {
            try
            {
                if (originalBytes.Length <= 3 * 1024 * 1024)
                {
                    return originalBytes;
                }

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
                    double maxDimension = 4000;
                    double scale = 1.0;

                    if (width > maxDimension || height > maxDimension)
                    {
                        scale = Math.Min(maxDimension / width, maxDimension / height);
                    }

                    if (scale < 1.0 || originalBytes.Length > 3 * 1024 * 1024)
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

                            if (resultBytes.Length > 4 * 1024 * 1024)
                            {
                                outStream.SetLength(0);
                                encoder.QualityLevel = 60;
                                encoder.Save(outStream);
                                resultBytes = outStream.ToArray();
                            }

                            LoggerService.Instance.LogInfo($"Image Optimized: Original {originalBytes.Length / 1024}KB -> Optimized {resultBytes.Length / 1024}KB (Scale: {scale:F2})", "TencentService.OptimizeImage");
                            return resultBytes;
                        }
                    }

                    return originalBytes;
                }
            }
            catch (Exception ex)
            {
                LoggerService.Instance.LogException(ex, "Image Optimization Failed", "TencentService");
                return originalBytes;
            }
        }

        #endregion
    }
}

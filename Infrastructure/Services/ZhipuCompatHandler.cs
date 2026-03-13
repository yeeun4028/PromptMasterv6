using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace PromptMasterv6.Infrastructure.Services
{
    public class ZhipuCompatHandler : DelegatingHandler
    {
        private readonly LoggerService _logger;

        public ZhipuCompatHandler(LoggerService logger)
        {
            _logger = logger;
            _logger.LogInfo("ZhipuCompatHandler instance created", "ZhipuCompatHandler");
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            _logger.LogInfo($"SendAsync called: URL={request.RequestUri}", "ZhipuCompatHandler");
            
            if (request.RequestUri != null && request.RequestUri.Host.Contains("bigmodel.cn", StringComparison.OrdinalIgnoreCase))
            {
                var uriStr = request.RequestUri.AbsoluteUri;
                
                if (uriStr.Contains("/v4/v1/") || uriStr.Contains("/v4//v1/"))
                {
                    var originalUri = uriStr;
                    
                    var newUriStr = uriStr.Replace("/v4//v1/", "/v4/").Replace("/v4/v1/", "/v4/");
                    
                    request.RequestUri = new Uri(newUriStr);
                    
                    _logger.LogInfo($"[ZhipuFix] Rewrote URL from {originalUri} to {newUriStr}", "ZhipuCompatHandler");
                }
            }
            
            if (request.RequestUri != null && request.RequestUri.Host.Contains("bigmodel.cn", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInfo($"[GLM Request] URL: {request.RequestUri}, Method: {request.Method}", "ZhipuCompatHandler");
            }
            
            var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
            
            if (request.RequestUri != null && request.RequestUri.Host.Contains("bigmodel.cn", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInfo($"[GLM Response] Status: {response.StatusCode}, ContentType: {response.Content.Headers.ContentType}", "ZhipuCompatHandler");
            }
            
            return response;
        }
    }
}

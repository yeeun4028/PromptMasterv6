using MediatR;
using PromptMasterv6.Infrastructure.Services;
using PromptMasterv6.Features.Shared.Models;
using PromptMasterv6.Features.ExternalTools.Messages;
using CommunityToolkit.Mvvm.Messaging;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PromptMasterv6.Features.Settings.ApiCredentials
{
    public static class SaveApiCredentialsFeature
    {
        /// <summary>
        /// 定义输入（必须实现 IRequest）
        /// </summary>
        public record Command(
            ApiProvider Provider,
            ServiceType ServiceType,
            string Name,
            string Key1,
            string Key2,
            string? BaseUrl = null) : IRequest<Result>;
        
        /// <summary>
        /// 定义输出
        /// </summary>
        public record Result(bool Success);

        /// <summary>
        /// 执行逻辑（必须实现 IRequestHandler）
        /// </summary>
        public class Handler : IRequestHandler<Command, Result>
        {
            private readonly SettingsService _settingsService;

            public Handler(SettingsService settingsService)
            {
                _settingsService = settingsService;
            }

            /// <summary>
            /// 必须带有 CancellationToken 以支持异步取消
            /// </summary>
            public Task<Result> Handle(Command request, CancellationToken cancellationToken)
            {
                var profile = _settingsService.Config.ApiProfiles.FirstOrDefault(p =>
                    p.Provider == request.Provider && p.ServiceType == request.ServiceType);

                if (profile == null)
                {
                    profile = new ApiProfile
                    {
                        Name = request.Name,
                        Provider = request.Provider,
                        ServiceType = request.ServiceType
                    };
                    _settingsService.Config.ApiProfiles.Add(profile);
                }

                profile.Key1 = request.Key1 ?? "";
                profile.Key2 = request.Key2 ?? "";
                if (!string.IsNullOrEmpty(request.BaseUrl))
                {
                    profile.BaseUrl = request.BaseUrl;
                }

                if (request.ServiceType == ServiceType.OCR && string.IsNullOrEmpty(_settingsService.Config.OcrProfileId))
                {
                    _settingsService.Config.OcrProfileId = profile.Id;
                }
                if (request.ServiceType == ServiceType.Translation && string.IsNullOrEmpty(_settingsService.Config.TranslateProfileId))
                {
                    _settingsService.Config.TranslateProfileId = profile.Id;
                }

                _settingsService.SaveConfig();
                WeakReferenceMessenger.Default.Send(new RefreshExternalToolsMessage());

                return Task.FromResult(new Result(true));
            }
        }
    }
}

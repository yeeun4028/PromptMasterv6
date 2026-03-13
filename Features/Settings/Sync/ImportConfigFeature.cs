using MediatR;
using PromptMasterv6.Infrastructure.Services;
using PromptMasterv6.Core.Messages;
using PromptMasterv6.Features.ExternalTools.Messages;
using CommunityToolkit.Mvvm.Messaging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace PromptMasterv6.Features.Settings.Sync
{
    public static class ImportConfigFeature
    {
        public record Command(string FilePath) : IRequest<Result>;
        public record Result(bool Success, string Message);

        public class Handler : IRequestHandler<Command, Result>
        {
            private readonly SettingsService _settingsService;

            public Handler(SettingsService settingsService)
            {
                _settingsService = settingsService;
            }

            public Task<Result> Handle(Command request, CancellationToken cancellationToken)
            {
                try
                {
                    _settingsService.ImportSettings(request.FilePath);
                    
                    WeakReferenceMessenger.Default.Send(new RefreshExternalToolsMessage());
                    
                    return Task.FromResult(new Result(true, "配置导入成功！"));
                }
                catch (Exception ex)
                {
                    return Task.FromResult(new Result(false, $"配置导入失败: {ex.Message}"));
                }
            }
        }
    }
}

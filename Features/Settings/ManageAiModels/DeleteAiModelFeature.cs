using MediatR;
using PromptMasterv6.Infrastructure.Services;
using PromptMasterv6.Features.Shared.Models;
using CommunityToolkit.Mvvm.Messaging;
using PromptMasterv6.Features.Settings.AiModels.Messages;
using System.Threading;
using System.Threading.Tasks;

namespace PromptMasterv6.Features.Settings.AiModels
{
    public static class DeleteAiModelFeature
    {
        /// <summary>
        /// 定义输入（必须实现 IRequest）
        /// </summary>
        public record Command(AiModelConfig Model) : IRequest<Result>;

        /// <summary>
        /// 定义输出
        /// </summary>
        public record Result(bool Success, string Message);

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
                var model = request.Model;
                var config = _settingsService.Config;

                var idx = config.SavedModels.IndexOf(model);
                if (idx >= 0)
                {
                    config.SavedModels.RemoveAt(idx);
                }

                if (config.ActiveModelId == model.Id)
                {
                    config.ActiveModelId = "";
                }

                _settingsService.SaveConfig();

                WeakReferenceMessenger.Default.Send(new AiModelDeletedMessage(model));

                return Task.FromResult(new Result(true, "模型删除成功"));
            }
        }
    }
}

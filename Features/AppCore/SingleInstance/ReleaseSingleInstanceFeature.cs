using MediatR;
using PromptMasterv6.Infrastructure.Services;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace PromptMasterv6.Features.AppCore.SingleInstance;

/// <summary>
/// 释放单实例锁功能
/// 负责在应用程序退出时释放互斥体
/// </summary>
public static class ReleaseSingleInstanceFeature
{
    // 1. 定义输入
    /// <summary>
    /// 释放单实例锁命令
    /// </summary>
    /// <param name="Mutex">互斥体引用</param>
    /// <param name="OwnsMutex">是否拥有互斥体</param>
    public record Command(Mutex? Mutex, bool OwnsMutex) : IRequest<Result>;

    // 2. 定义输出
    /// <summary>
    /// 释放结果
    /// </summary>
    /// <param name="Success">是否成功</param>
    /// <param name="Message">结果消息</param>
    public record Result(bool Success, string Message);

    // 3. 执行逻辑
    /// <summary>
    /// 释放单实例锁处理器
    /// </summary>
    public class Handler : IRequestHandler<Command, Result>
    {
        private readonly LoggerService _logger;

        /// <summary>
        /// 构造函数 - 只注入当前 Feature 绝对需要的服务
        /// </summary>
        public Handler(LoggerService logger)
        {
            _logger = logger;
        }

        public Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            try
            {
                if (request.Mutex == null)
                {
                    return Task.FromResult(new Result(true, "互斥体为空，无需释放"));
                }

                if (request.OwnsMutex)
                {
                    _logger.LogInfo("Releasing single instance mutex...", "ReleaseSingleInstanceFeature.Handle");
                    request.Mutex.ReleaseMutex();
                    _logger.LogInfo("Single instance mutex released.", "ReleaseSingleInstanceFeature.Handle");
                }

                request.Mutex.Dispose();

                return Task.FromResult(new Result(true, "单实例锁释放成功"));
            }
            catch (Exception ex)
            {
                _logger.LogException(ex, "Failed to release single instance mutex", "ReleaseSingleInstanceFeature.Handle");
                return Task.FromResult(new Result(false, $"释放失败: {ex.Message}"));
            }
        }
    }
}

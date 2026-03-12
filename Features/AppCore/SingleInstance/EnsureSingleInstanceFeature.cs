ceusing MediatR;
using PromptMasterv6.Infrastructure.Services;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace PromptMasterv6.Features.AppCore.SingleInstance;

/// <summary>
/// 确保单实例运行功能
/// 负责检查是否已有实例运行，并激活现有实例
/// </summary>
public static class EnsureSingleInstanceFeature
{
    // 1. 定义输入
    /// <summary>
    /// 确保单实例命令
    /// </summary>
    /// <param name="MutexName">互斥体名称</param>
    /// <param name="WindowTitle">窗口标题（用于激活现有实例）</param>
    public record Command(string MutexName, string WindowTitle) : IRequest<Result>;

    // 2. 定义输出
    /// <summary>
    /// 单实例检查结果
    /// </summary>
    /// <param name="IsFirstInstance">是否为第一个实例</param>
    /// <param name="Message">结果消息</param>
    /// <param name="Mutex">互斥体引用（需要调用者持有并在退出时释放）</param>
    /// <param name="OwnsMutex">是否拥有互斥体</param>
    public record Result(bool IsFirstInstance, string Message, Mutex? Mutex, bool OwnsMutex);

    // 3. 执行逻辑
    /// <summary>
    /// 单实例检查处理器
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
                _logger.LogInfo($"Checking for existing instance with mutex: {request.MutexName}", "EnsureSingleInstanceFeature.Handle");

                // 尝试创建或获取互斥体
                bool createdNew;
                var mutex = new Mutex(true, request.MutexName, out createdNew);

                if (!createdNew)
                {
                    // 已有实例运行，激活现有实例
                    _logger.LogInfo("Another instance is already running. Activating existing instance.", "EnsureSingleInstanceFeature.Handle");
                    ActivateExistingInstance(request.WindowTitle);

                    // 释放刚获取的互斥体引用
                    mutex.Dispose();

                    return Task.FromResult(new Result(false, "已有实例运行，已激活现有实例", null, false));
                }

                _logger.LogInfo("Single instance mutex acquired successfully.", "EnsureSingleInstanceFeature.Handle");
                return Task.FromResult(new Result(true, "成功获取单实例锁", mutex, true));
            }
            catch (Exception ex)
            {
                _logger.LogException(ex, "Failed to ensure single instance", "EnsureSingleInstanceFeature.Handle");
                return Task.FromResult(new Result(false, $"单实例检查失败: {ex.Message}", null, false));
            }
        }

        private void ActivateExistingInstance(string windowTitle)
        {
            try
            {
                IntPtr hWnd = NativeMethods.FindWindow(null, windowTitle);

                if (hWnd != IntPtr.Zero)
                {
                    // 如果窗口最小化，先恢复
                    if (NativeMethods.IsIconic(hWnd))
                    {
                        NativeMethods.ShowWindow(hWnd, NativeMethods.SW_RESTORE);
                    }

                    // 激活窗口
                    NativeMethods.SetForegroundWindow(hWnd);
                    _logger.LogInfo($"Activated existing instance window: {windowTitle}", "EnsureSingleInstanceFeature.ActivateExistingInstance");
                }
                else
                {
                    _logger.LogWarning($"Could not find window with title: {windowTitle}", "EnsureSingleInstanceFeature.ActivateExistingInstance");
                }
            }
            catch (Exception ex)
            {
                _logger.LogException(ex, "Failed to activate existing instance", "EnsureSingleInstanceFeature.ActivateExistingInstance");
            }
        }
    }
}

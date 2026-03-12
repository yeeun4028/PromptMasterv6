using MediatR;
using PromptMasterv6.Infrastructure.Services;

namespace PromptMasterv6.Features.Settings.Sync;

/// <summary>
/// 选择导入路径功能
/// </summary>
public static class SelectImportPathFeature
{
    // 1. 定义输入
    public record Command(
        string FileExtension = "zip"
    ) : IRequest<Result>;

    // 2. 定义输出
    public record Result(
        bool Success,
        string? SelectedPath,
        bool UserCancelled = false
    );

    // 3. 执行逻辑
    public class Handler : IRequestHandler<Command, Result>
    {
        private readonly LoggerService _logger;

        public Handler(LoggerService logger)
        {
            _logger = logger;
        }

        public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            return await Task.Run(() =>
            {
                try
                {
                    var dialog = new Microsoft.Win32.OpenFileDialog
                    {
                        Title = "导入配置",
                        DefaultExt = $".{request.FileExtension}",
                        Filter = $"{request.FileExtension.ToUpper()} files (*.{request.FileExtension})|*.{request.FileExtension}|All files (*.*)|*.*"
                    };

                    // 获取当前活动窗口作为所有者
                    System.Windows.Window? owner = null;
                    System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                    {
                        owner = System.Windows.Application.Current.Windows
                            .OfType<System.Windows.Window>()
                            .FirstOrDefault(w => w.IsActive);
                    });

                    var result = dialog.ShowDialog(owner);

                    if (result == true)
                    {
                        _logger.LogInfo($"用户选择导入路径: {dialog.FileName}", "SelectImportPathFeature.Handle");
                        return new Result(true, dialog.FileName);
                    }
                    else
                    {
                        return new Result(false, null, true);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogException(ex, "文件对话框初始化失败", "SelectImportPathFeature.Handle");
                    return new Result(false, null);
                }
            });
        }
    }
}

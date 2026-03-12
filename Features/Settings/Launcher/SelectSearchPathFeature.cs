using MediatR;
using PromptMasterv6.Infrastructure.Services;

namespace PromptMasterv6.Features.Settings.Launcher;

/// <summary>
/// 选择搜索路径功能
/// </summary>
public static class SelectSearchPathFeature
{
    // 1. 定义输入
    public record Command() : IRequest<Result>;

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
                    var dialog = new System.Windows.Forms.FolderBrowserDialog
                    {
                        Description = "选择要添加的搜索文件夹",
                        UseDescriptionForTitle = true,
                        ShowNewFolderButton = false
                    };

                    var result = dialog.ShowDialog();

                    if (result == System.Windows.Forms.DialogResult.OK)
                    {
                        _logger.LogInfo($"用户选择搜索路径: {dialog.SelectedPath}", "SelectSearchPathFeature.Handle");
                        return new Result(true, dialog.SelectedPath);
                    }
                    else
                    {
                        return new Result(false, null, true);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogException(ex, "文件夹对话框初始化失败", "SelectSearchPathFeature.Handle");
                    return new Result(false, null);
                }
            }, cancellationToken);
        }
    }
}

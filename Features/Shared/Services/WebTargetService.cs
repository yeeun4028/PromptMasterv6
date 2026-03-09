using PromptMasterv6.Core.Interfaces;
using PromptMasterv6.Core.Models;
using PromptMasterv6.Infrastructure.Services;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace PromptMasterv6.Features.Shared.Services;

public class WebTargetService : IWebTargetService
{
    private readonly ClipboardService _clipboardService;
    private readonly IDialogService _dialogService;

    public WebTargetService(ClipboardService clipboardService, IDialogService dialogService)
    {
        _clipboardService = clipboardService;
        _dialogService = dialogService;
    }

    public async Task OpenWebTargetAsync(WebTarget target, string content, bool hideMainWindow = true)
    {
        if (target == null || string.IsNullOrWhiteSpace(content))
        {
            _dialogService.ShowAlert("目标或内容为空。", "无法打开");
            return;
        }

        try
        {
            bool supportsUrlParam = target.UrlTemplate.Contains("{0}");
            bool useClipboard = !supportsUrlParam || content.Length > 2000;
            string url;

            if (useClipboard)
            {
                _clipboardService.SetClipboard(content);
                try { url = string.Format(target.UrlTemplate, ""); }
                catch { url = target.UrlTemplate.Split('?')[0]; }
                _dialogService.ShowAlert("提示词过长，已复制到剪贴板，请手动粘贴。", "提示");
            }
            else
            {
                url = string.Format(target.UrlTemplate, Uri.EscapeDataString(content));
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });

            if (hideMainWindow && System.Windows.Application.Current.MainWindow != null)
            {
                System.Windows.Application.Current.MainWindow.Hide();
            }
        }
        catch (Exception ex)
        {
            LoggerService.Instance.LogException(ex, "OpenWebTarget Failed", "WebTargetService.OpenWebTargetAsync");
            _dialogService.ShowAlert($"打开网页失败: {ex.Message}", "错误");
        }

        await Task.CompletedTask;
    }

    public async Task SendToDefaultTargetAsync(string content, AppConfig config)
    {
        var targetName = config.DefaultWebTargetName;
        var target = config.WebDirectTargets.FirstOrDefault(t => t.Name == targetName);

        if (target != null)
        {
            if (!target.IsEnabled)
            {
                _dialogService.ShowAlert($"默认目标 '{targetName}' 已被禁用，请在设置中启用。", "目标不可用");
                return;
            }
            await OpenWebTargetAsync(target, content);
        }
        else
        {
            target = config.WebDirectTargets.FirstOrDefault(t => t.Name == "Gemini" && t.IsEnabled)
                     ?? config.WebDirectTargets.FirstOrDefault(t => t.IsEnabled);

            if (target != null)
            {
                await OpenWebTargetAsync(target, content);
            }
            else
            {
                _dialogService.ShowAlert($"未找到默认网页目标: {targetName}，且无可用的备选目标。", "配置错误");
            }
        }
    }
}

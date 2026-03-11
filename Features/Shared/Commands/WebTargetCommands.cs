using MediatR;
using PromptMasterv6.Infrastructure.Services;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PromptMasterv6.Features.Shared.Commands;

public record ExecuteWebTargetCommand(
    Models.WebTarget Target,
    string Content,
    bool HideMainWindow = true) : IRequest;

public class ExecuteWebTargetHandler : IRequestHandler<ExecuteWebTargetCommand>
{
    private readonly ClipboardService _clipboardService;
    private readonly DialogService _dialogService;
    private readonly LoggerService _logger;

    public ExecuteWebTargetHandler(
        ClipboardService clipboardService,
        DialogService dialogService,
        LoggerService logger)
    {
        _clipboardService = clipboardService;
        _dialogService = dialogService;
        _logger = logger;
    }

    public async Task Handle(ExecuteWebTargetCommand request, CancellationToken cancellationToken)
    {
        if (request.Target == null || string.IsNullOrWhiteSpace(request.Content))
        {
            _dialogService.ShowAlert("目标或内容为空。", "无法打开");
            return;
        }

        try
        {
            bool supportsUrlParam = request.Target.UrlTemplate.Contains("{0}");
            bool useClipboard = !supportsUrlParam || request.Content.Length > 8000;
            string url;

            if (useClipboard)
            {
                _clipboardService.SetClipboard(request.Content);
                try { url = string.Format(request.Target.UrlTemplate, ""); }
                catch { url = request.Target.UrlTemplate.Split('?')[0]; }
                _dialogService.ShowAlert("提示词过长，已复制到剪贴板，请手动粘贴。", "提示");
            }
            else
            {
                url = string.Format(request.Target.UrlTemplate, Uri.EscapeDataString(request.Content));
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });

            if (request.HideMainWindow && System.Windows.Application.Current.MainWindow != null)
            {
                System.Windows.Application.Current.MainWindow.Hide();
            }
        }
        catch (Exception ex)
        {
            _logger.LogException(ex, "ExecuteWebTarget Failed", "ExecuteWebTargetHandler.Handle");
            _dialogService.ShowAlert($"打开网页失败: {ex.Message}", "错误");
        }

        await Task.CompletedTask;
    }
}

public record SendToDefaultTargetCommand(
    string Content,
    ObservableCollection<Models.WebTarget> Targets,
    string? DefaultTargetName) : IRequest;

public class SendToDefaultTargetHandler : IRequestHandler<SendToDefaultTargetCommand>
{
    private readonly IMediator _mediator;
    private readonly DialogService _dialogService;

    public SendToDefaultTargetHandler(
        IMediator mediator,
        DialogService dialogService)
    {
        _mediator = mediator;
        _dialogService = dialogService;
    }

    public async Task Handle(SendToDefaultTargetCommand request, CancellationToken cancellationToken)
    {
        var targetName = request.DefaultTargetName;
        var target = request.Targets?.FirstOrDefault(t => t.Name == targetName);

        if (target != null)
        {
            if (!target.IsEnabled)
            {
                _dialogService.ShowAlert($"默认目标 '{targetName}' 已被禁用，请在设置中启用。", "目标不可用");
                return;
            }
            await _mediator.Send(new ExecuteWebTargetCommand(target, request.Content));
        }
        else
        {
            target = request.Targets?.FirstOrDefault(t => t.Name == "Gemini" && t.IsEnabled)
                     ?? request.Targets?.FirstOrDefault(t => t.IsEnabled);

            if (target != null)
            {
                await _mediator.Send(new ExecuteWebTargetCommand(target, request.Content));
            }
            else
            {
                _dialogService.ShowAlert($"未找到默认网页目标: {targetName}，且无可用的备选目标。", "配置错误");
            }
        }
    }
}

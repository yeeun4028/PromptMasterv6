using MediatR;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace PromptMasterv6.Features.Main.Search;

public record SearchOnGitHubCommand(string Query) : IRequest;

public class SearchOnGitHubHandler : IRequestHandler<SearchOnGitHubCommand>
{
    private readonly IDialogService _dialogService;

    public SearchOnGitHubHandler(IDialogService dialogService)
    {
        _dialogService = dialogService;
    }

    public Task Handle(SearchOnGitHubCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Query))
        {
            _dialogService.ShowAlert("请输入要搜索的内容。", "输入为空");
            return Task.CompletedTask;
        }

        try
        {
            var url = $"https://github.com/search?q={System.Uri.EscapeDataString(request.Query)}";
            Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
        }
        catch (System.Exception ex)
        {
            Infrastructure.Services.LoggerService.Instance.LogException(ex, "SearchOnGitHub Failed", "SearchOnGitHubHandler");
            _dialogService.ShowAlert($"打开 GitHub 失败: {ex.Message}", "错误");
        }

        return Task.CompletedTask;
    }
}

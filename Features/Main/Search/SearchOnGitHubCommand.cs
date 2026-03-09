using MediatR;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using PromptMasterv6.Infrastructure.Services;

namespace PromptMasterv6.Features.Main.Search;

public record SearchOnGitHubCommand(string Query) : IRequest;

public class SearchOnGitHubHandler : IRequestHandler<SearchOnGitHubCommand>
{
    private readonly DialogService _dialogService;

    public SearchOnGitHubHandler(DialogService dialogService)
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

        var url = $"https://github.com/search?q={System.Uri.EscapeDataString(request.Query)}";
        Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });

        return Task.CompletedTask;
    }
}

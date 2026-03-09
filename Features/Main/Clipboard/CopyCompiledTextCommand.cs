using MediatR;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using PromptMasterv6.Infrastructure.Services;

namespace PromptMasterv6.Features.Main.Clipboard;

public record CopyCompiledTextCommand(
    string? Content,
    ObservableCollection<VariableItem> Variables,
    string AdditionalInput) : IRequest;

public class CopyCompiledTextHandler : IRequestHandler<CopyCompiledTextCommand>
{
    private readonly VariableService _variableService;
    private readonly ClipboardService _clipboardService;

    public CopyCompiledTextHandler(
        VariableService variableService,
        ClipboardService clipboardService)
    {
        _variableService = variableService;
        _clipboardService = clipboardService;
    }

    public Task Handle(CopyCompiledTextCommand request, CancellationToken cancellationToken)
    {
        var text = _variableService.CompileContent(request.Content, request.Variables, request.AdditionalInput);
        if (string.IsNullOrWhiteSpace(text)) return Task.CompletedTask;
        _clipboardService.SetClipboard(text);
        return Task.CompletedTask;
    }
}

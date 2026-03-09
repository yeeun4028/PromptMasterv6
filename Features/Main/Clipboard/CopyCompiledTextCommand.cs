using MediatR;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;

namespace PromptMasterv6.Features.Main.Clipboard;

public record CopyCompiledTextCommand(
    string? Content,
    ObservableCollection<VariableItem> Variables,
    string AdditionalInput) : IRequest;

public class CopyCompiledTextHandler : IRequestHandler<CopyCompiledTextCommand>
{
    private readonly IVariableService _variableService;
    private readonly IClipboardService _clipboardService;

    public CopyCompiledTextHandler(
        IVariableService variableService,
        IClipboardService clipboardService)
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

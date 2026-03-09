using MediatR;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PromptMasterv6.Infrastructure.Services;

namespace PromptMasterv6.Features.Shared.Commands;

public record CopyCompiledTextCommand(
    string? Content,
    Dictionary<string, string> Variables,
    string? AdditionalInput = null) : IRequest;

public class CopyCompiledTextHandler : IRequestHandler<CopyCompiledTextCommand>
{
    private readonly IMediator _mediator;
    private readonly ClipboardService _clipboardService;

    public CopyCompiledTextHandler(
        IMediator mediator,
        ClipboardService clipboardService)
    {
        _mediator = mediator;
        _clipboardService = clipboardService;
    }

    public async Task Handle(CopyCompiledTextCommand request, CancellationToken cancellationToken)
    {
        var text = await _mediator.Send(new Queries.CompileContentQuery(
            request.Content, 
            request.Variables, 
            request.AdditionalInput));
        
        if (string.IsNullOrWhiteSpace(text)) return;
        _clipboardService.SetClipboard(text);
    }
}

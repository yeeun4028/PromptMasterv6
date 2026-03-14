using MediatR;
using PromptMasterv6.Features.Ai.TestConnection;
using System.Threading;
using System.Threading.Tasks;

namespace PromptMasterv6.Features.AiModels.TestConnection;

public static class TestAiConnectionFeature
{
    public record Command(string ApiKey, string BaseUrl, string ModelName, bool UseProxy) : IRequest<Result>;

    public record Result(bool Success, string Message, long? ResponseTimeMs, string DisplayMessage);

    public class Handler : IRequestHandler<Command, Result>
    {
        private readonly IMediator _mediator;

        public Handler(IMediator mediator)
        {
            _mediator = mediator;
        }

        public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
                var result = await _mediator.Send(new Features.Ai.TestConnection.TestConnectionFeature.Command(
                    request.ApiKey, 
                    request.BaseUrl, 
                    request.ModelName, 
                    request.UseProxy), cancellationToken);

                var displayMessage = result.Success && result.ResponseTimeMs.HasValue
                    ? $"{result.Message} ({result.ResponseTimeMs}ms)"
                    : result.Message;

                return new Result(result.Success, result.Message, result.ResponseTimeMs, displayMessage);
        }
    }
}

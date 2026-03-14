using MediatR;
using PromptMasterv6.Features.Shared.Models;

namespace PromptMasterv6.Features.AiModels.Events;

public sealed record AiModelAddedEvent(AiModelConfig AddedModel) : INotification;

public sealed record AiModelDeletedEvent(AiModelConfig DeletedModel) : INotification;

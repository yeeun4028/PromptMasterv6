using MediatR;
using PromptMasterv6.Features.Shared.Models;
using System.Collections.Generic;

namespace PromptMasterv6.Features.Workspace.FileList;

public record GetFilesByFolderQuery(string? FolderId) : IRequest<List<PromptItem>>;

using MediatR;
using PromptMasterv6.Infrastructure.Services;
using PromptMasterv6.Core.Messages;
using CommunityToolkit.Mvvm.Messaging;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PromptMasterv6.Features.Settings.Sync
{
    public static class ManualLocalRestoreFeature
    {
        public record Command(string FilePath) : IRequest<Result>;
        public record Result(bool Success, string Message);

        public class Handler : IRequestHandler<Command, Result>
        {
            private readonly FileDataService _localDataService;
            private readonly ISessionState _sessionState;
            private readonly LoggerService _logger;

            public Handler(FileDataService localDataService, ISessionState sessionState, LoggerService logger)
            {
                _localDataService = localDataService;
                _sessionState = sessionState;
                _logger = logger;
            }

            public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
            {
                try
                {
                    var data = await _localDataService.RestoreLocalBackupAsync(request.FilePath);
                    if (data == null)
                    {
                        return new Result(false, "读取备份文件失败");
                    }

                    ApplyRestoredData(data);

                    return new Result(true, "本地恢复成功");
                }
                catch (Exception ex)
                {
                    _logger.LogException(ex, "Failed to restore from local backup", "ManualLocalRestoreFeature.Handle");
                    return new Result(false, $"恢复失败: {ex.Message}");
                }
            }

            private void ApplyRestoredData(AppData data)
            {
                _sessionState.Files.Clear();
                _sessionState.Folders.Clear();

                if (data.Folders != null && data.Folders.Count > 0)
                {
                    foreach (var folder in data.Folders)
                    {
                        _sessionState.Folders.Add(folder);
                    }
                    _sessionState.SelectedFolder = _sessionState.Folders.FirstOrDefault();
                }
                else
                {
                    var defaultFolder = new Features.Shared.Models.FolderItem { Name = "默认" };
                    _sessionState.Folders.Add(defaultFolder);
                    _sessionState.SelectedFolder = defaultFolder;
                }

                if (data.Files != null && data.Files.Count > 0)
                {
                    foreach (var file in data.Files)
                    {
                        if (string.IsNullOrWhiteSpace(file.FolderId) && _sessionState.SelectedFolder != null)
                        {
                            file.FolderId = _sessionState.SelectedFolder.Id;
                        }
                        _sessionState.Files.Add(file);
                    }
                }

                _sessionState.RefreshFilesView();
                WeakReferenceMessenger.Default.Send(new ReloadDataMessage());
            }
        }
    }
}

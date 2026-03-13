using MediatR;
using PromptMasterv6.Infrastructure.Services;
using PromptMasterv6.Core.Messages;
using PromptMasterv6.Features.ExternalTools.Messages;
using CommunityToolkit.Mvvm.Messaging;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PromptMasterv6.Features.Settings.Sync
{
    public static class ManualRestoreFeature
    {
        public record Command() : IRequest<Result>;
        public record Result(bool Success, string Message, int FolderCount = 0, int FileCount = 0);

        public class Handler : IRequestHandler<Command, Result>
        {
            private readonly IDataService _dataService;
            private readonly ISessionState _sessionState;
            private readonly LoggerService _logger;

            public Handler(IDataService dataService, ISessionState sessionState, LoggerService logger)
            {
                _dataService = dataService;
                _sessionState = sessionState;
                _logger = logger;
            }

            public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
            {
                try
                {
                    var data = await _dataService.LoadAsync();

                    if (data == null || ((data.Folders?.Count ?? 0) == 0 && (data.Files?.Count ?? 0) == 0))
                    {
                        return new Result(false, "云端没有数据可恢复 (或连接失败)");
                    }

                    ApplyRestoredData(data);

                    _logger.LogInfo($"Restored {data.Folders?.Count ?? 0} folders and {data.Files?.Count ?? 0} files", "ManualRestoreFeature.Handle");

                    return new Result(true, $"成功恢复 {data.Folders?.Count ?? 0} 个文件夹和 {data.Files?.Count ?? 0} 个提示词",
                        data.Folders?.Count ?? 0, data.Files?.Count ?? 0);
                }
                catch (Exception ex)
                {
                    _logger.LogException(ex, "Failed to restore from cloud", "ManualRestoreFeature.Handle");
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

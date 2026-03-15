using MediatR;
using PromptMasterv6.Core.Interfaces;
using PromptMasterv6.Features.Shared.Models;
using PromptMasterv6.Features.Workspace.State;
using PromptMasterv6.Features.Workspace.FilterFiles;
using PromptMasterv6.Features.Shared.Messages;
using PromptMasterv6.Infrastructure.Services;
using CommunityToolkit.Mvvm.Messaging;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace PromptMasterv6.Features.Workspace.LoadWorkspaceData;

public static class LoadWorkspaceDataFeature
{
    public record Command() : IRequest<Result>;
    public record Result(bool Success);

    public class Handler : IRequestHandler<Command, Result>
    {
        private readonly IDataService _dataService;
        private readonly IDataService _localDataService;
        private readonly IWorkspaceState _state;
        private readonly DialogService _dialogService;
        private readonly IMediator _mediator;

        public Handler(
            [Microsoft.Extensions.DependencyInjection.FromKeyedServices("cloud")] IDataService dataService,
            [Microsoft.Extensions.DependencyInjection.FromKeyedServices("local")] IDataService localDataService,
            IWorkspaceState state,
            DialogService dialogService,
            IMediator mediator)
        {
            _dataService = dataService;
            _localDataService = localDataService;
            _state = state;
            _dialogService = dialogService;
            _mediator = mediator;
        }

        public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            AppData? data = null;

            try
            {
                data = await _dataService.LoadAsync();
            }
            catch
            {
            }

            if ((data?.Files?.Count ?? 0) == 0 && (data?.Folders?.Count ?? 0) == 0)
            {
                try
                {
                    data = await _localDataService.LoadAsync();
                }
                catch
                {
                }
            }

            if (data == null)
            {
                _dialogService.ShowAlert("无法加载数据", "错误");
                return new Result(false);
            }

            var files = data.Files ?? new List<PromptItem>();
            var folders = data.Folders ?? new List<FolderItem>();

            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                _state.Files.Clear();
                _state.Folders.Clear();

                foreach (var folder in folders)
                {
                    _state.Folders.Add(folder);
                }

                foreach (var file in files)
                {
                    _state.Files.Add(file);
                }

                _mediator.Send(new FilterFilesFeature.Command(_state.FilesView, _state.SelectedFolder), cancellationToken);
                _state.FilesView?.Refresh();

                if (_state.FilesView != null && !_state.FilesView.IsEmpty)
                {
                    var firstItem = _state.FilesView.Cast<PromptItem>().FirstOrDefault();
                    if (firstItem != null)
                    {
                        _state.SelectedFile = firstItem;
                        WeakReferenceMessenger.Default.Send(new FileSelectedMessage(firstItem));
                    }
                }

                _state.IsDirty = false;
            });

            return new Result(true);
        }
    }
}

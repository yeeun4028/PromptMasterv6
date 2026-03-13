using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using MediatR;
using PromptMasterv6.Core.Messages;
using PromptMasterv6.Infrastructure.Services;

namespace PromptMasterv6.Features.Main.Tray;

public partial class TrayViewModel : ObservableObject
{
    private readonly IMediator _mediator;
    private readonly LoggerService _logger;
    private readonly SettingsService _settingsService;

    public event Action? ToggleWindowRequested;
    public event Func<Task>? ExitRequested;
    public Func<bool>? IsDirtyCheck { get; set; }

    public TrayViewModel(
        IMediator mediator,
        LoggerService logger,
        SettingsService settingsService)
    {
        _mediator = mediator;
        _logger = logger;
        _settingsService = settingsService;
    }

    [RelayCommand]
    private void ToggleVisibility()
    {
        ToggleWindowRequested?.Invoke();
    }

    [RelayCommand]
    private async Task ManualBackup()
    {
        WeakReferenceMessenger.Default.Send(new RequestBackupMessage());
    }

    [RelayCommand]
    private async Task OpenSettings()
    {
        await _mediator.Send(new OpenSettingsFeature.Command());
    }

    [RelayCommand]
    private async Task Exit()
    {
        if (ExitRequested != null)
        {
            await ExitRequested.Invoke();
        }
    }
}

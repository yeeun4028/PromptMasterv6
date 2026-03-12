using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using PromptMasterv6.Infrastructure.Services;
using System;
using System.Threading.Tasks;

namespace PromptMasterv6.Features.Settings.Launcher;

public partial class LauncherSettingsViewModel : ObservableObject
{
    private readonly IMediator _mediator;
    private readonly SettingsService _settingsService;
    private readonly LoggerService _logger;

    public AppConfig Config => _settingsService.Config;

    public LauncherSettingsViewModel(
        IMediator mediator,
        SettingsService settingsService,
        LoggerService logger)
    {
        _mediator = mediator;
        _settingsService = settingsService;
        _logger = logger;
    }

    [RelayCommand]
    private async Task AddLauncherSearchPath()
    {
        try
        {
            // 1. 选择搜索路径
            var selectResult = await _mediator.Send(new SelectSearchPathFeature.Command());

            if (!selectResult.Success || selectResult.UserCancelled)
            {
                return;  // 用户取消或失败
            }

            // 2. 添加搜索路径
            var result = await _mediator.Send(new AddSearchPathFeature.Command(selectResult.SelectedPath!));
            if (!result.Success && result.ErrorMessage != null)
            {
                _logger.LogInfo($"Add search path skipped: {result.ErrorMessage}", "LauncherSettingsViewModel.AddLauncherSearchPath");
            }
        }
        catch (Exception ex)
        {
            _logger.LogException(ex, "Failed to add launcher search path", "LauncherSettingsViewModel.AddLauncherSearchPath");
        }
    }

    [RelayCommand]
    private async Task RemoveLauncherSearchPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        await _mediator.Send(new RemoveSearchPathFeature.Command(path));
    }
}

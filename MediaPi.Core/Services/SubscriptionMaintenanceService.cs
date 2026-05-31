// Copyright (C) 2025-2026 sw.consulting
// This file is a part of Media Pi backend

using MediaPi.Core.RestModels;
using MediaPi.Core.Services.Interfaces;

namespace MediaPi.Core.Services;

public class SubscriptionMaintenanceService(
    IServiceScopeFactory scopeFactory,
    ISubscriptionTimeService timeService,
    ILogger<SubscriptionMaintenanceService> logger) : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    private readonly ISubscriptionTimeService _timeService = timeService;
    private readonly ILogger<SubscriptionMaintenanceService> _logger = logger;

    public async Task<PlaylistCleanupResult> RunCleanupOnceAsync(CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var playlistAccessService = scope.ServiceProvider.GetRequiredService<IPlaylistAccessService>();
        var result = await playlistAccessService.RemoveCurrentInvalidPlaylistItemsAsync(ct);
        _logger.LogInformation(
            "Subscription maintenance removed {RemovedItemCount} playlist items from {AffectedPlaylistCount} playlists for {AffectedVideoCount} videos.",
            result.RemovedItemCount,
            result.AffectedPlaylistCount,
            result.AffectedVideoCount);
        return result;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("SubscriptionMaintenanceService started.");

        try
        {
            await RunCleanupOnceAsync(stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                var delay = GetDelayUntilNextLocalMidnight();
                await Task.Delay(delay, stoppingToken);
                await RunCleanupOnceAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "SubscriptionMaintenanceService encountered a fatal error and is stopping.");
        }
        finally
        {
            _logger.LogInformation("SubscriptionMaintenanceService stopped.");
        }
    }

    internal TimeSpan GetDelayUntilNextLocalMidnight()
    {
        var localNow = _timeService.LocalNow;
        var nextLocalMidnight = localNow.Date.AddDays(1);
        var delay = nextLocalMidnight - localNow;
        return delay <= TimeSpan.Zero ? TimeSpan.FromSeconds(1) : delay;
    }
}

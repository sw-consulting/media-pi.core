// Copyright (C) 2025-2026 sw.consulting
// This file is a part of Media Pi backend

using MediaPi.Core.Data;
using MediaPi.Core.Models;
using MediaPi.Core.RestModels;
using MediaPi.Core.Services.Interfaces;
using MediaPi.Core.Services.Models;
using MediaPi.Core.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Threading.Channels;

namespace MediaPi.Core.Services;

public class DeviceMonitoringService : BackgroundService, IDeviceMonitoringService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly DeviceMonitorSettings _settings;
    private readonly ConcurrentDictionary<int, DeviceStatusSnapshot> _snapshot = new();
    private readonly ILogger<DeviceMonitoringService> _logger;
    private readonly DeviceEventsService _deviceEventsService;
    private readonly IMediaPiAgentClient _agentClient;
    private readonly ConcurrentDictionary<Guid, Channel<DeviceStatusEvent>> _subscribers = new();
    private readonly SemaphoreSlim _pollWakeSignal = new(0);

    public DeviceMonitoringService(
        IServiceScopeFactory scopeFactory,
        IOptions<DeviceMonitorSettings> options,
        ILogger<DeviceMonitoringService> logger,
        DeviceEventsService deviceEventsService,
        IMediaPiAgentClient agentClient)
    {
        _scopeFactory = scopeFactory;
        _settings = options.Value;
        _logger = logger;
        _deviceEventsService = deviceEventsService;
        _agentClient = agentClient;
        _deviceEventsService.DeviceDeleted += OnDeviceDeleted;
    }

    public IReadOnlyDictionary<int, DeviceStatusSnapshot> Snapshot => _snapshot;

    public bool TryGetStatus(int deviceId, out DeviceStatusSnapshot? status) => _snapshot.TryGetValue(deviceId, out status!);

    public bool TryGetStatusItem(int deviceId, out DeviceStatusItem? status)
    {
        if (_snapshot.TryGetValue(deviceId, out var snap))
        {
            status = new DeviceStatusItem(deviceId, snap);
            return true;
        }
        status = null;
        return false;
    }

    public IAsyncEnumerable<DeviceStatusEvent> Subscribe(CancellationToken token = default)
    {
        var channel = Channel.CreateUnbounded<DeviceStatusEvent>();
        var id = Guid.NewGuid();
        _subscribers[id] = channel;
        _pollWakeSignal.Release();

        foreach (var kvp in _snapshot)
        {
            channel.Writer.TryWrite(new DeviceStatusEvent(kvp.Key, kvp.Value));
        }

        token.Register(() =>
        {
            _subscribers.TryRemove(id, out _);
            channel.Writer.TryComplete();
            _pollWakeSignal.Release();
        });

        return channel.Reader.ReadAllAsync(token);
    }

    public async Task<DeviceStatusSnapshot?> Test(int deviceId, CancellationToken token = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var device = await db.Devices.AsNoTracking().FirstOrDefaultAsync(d => d.Id == deviceId, token);
        if (device is null)
            return null;

        var (snap, probe) = await ProbeDevice(device, token);
        db.DeviceProbes.Add(probe);
        try
        {
            await db.SaveChangesAsync(token);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving device probe result to database for device {DeviceId}", deviceId);
        }
        return snap;
    }

    private void OnDeviceDeleted(int deviceId)
    {
        if (_snapshot.TryRemove(deviceId, out _))
        {
            _logger.LogInformation("Device {DeviceId} removed from monitoring snapshot via event notification.", deviceId);
            Broadcast(new DeviceStatusEvent(deviceId, new DeviceStatusSnapshot
            {
                IpAddress = string.Empty,
                IsOnline = false,
                LastChecked = DateTime.UtcNow,
                ConnectLatencyMs = 0,
                TotalLatencyMs = 0,
                SoftwareVersion = null,
                PlaybackServiceStatus = null,
                PlaylistUploadServiceStatus = null,
                VideoUploadServiceStatus = null
            }));
        }
    }

    private void Broadcast(DeviceStatusEvent evt)
    {
        foreach (var subscriber in _subscribers.Values)
        {
            subscriber.Writer.TryWrite(evt);
        }
    }

    private async Task<(DeviceStatusSnapshot Snapshot, DeviceProbe Probe)> ProbeDevice(Device device, CancellationToken token)
    {
        var probeResult = await Probe(device, token);
        var snap = new DeviceStatusSnapshot
        {
            IpAddress = device.IpAddress,
            IsOnline = probeResult.IsOnline,
            LastChecked = DateTime.UtcNow,
            ConnectLatencyMs = probeResult.ConnectMs,
            TotalLatencyMs = probeResult.TotalMs,
            SoftwareVersion = probeResult.SoftwareVersion,
            PlaybackServiceStatus = probeResult.ServiceStatus?.PlaybackServiceStatus,
            PlaylistUploadServiceStatus = probeResult.ServiceStatus?.PlaylistUploadServiceStatus,
            VideoUploadServiceStatus = probeResult.ServiceStatus?.VideoUploadServiceStatus
        };
        _snapshot[device.Id] = snap;
        Broadcast(new DeviceStatusEvent(device.Id, snap));
        _logger.LogInformation("Probed device {DeviceId} ({IpAddress}): Online={IsOnline}, ConnectMs={ConnectMs}, TotalMs={TotalMs}, Version={SoftwareVersion}",
            device.Id, device.IpAddress, snap.IsOnline, snap.ConnectLatencyMs, snap.TotalLatencyMs, snap.SoftwareVersion ?? "unknown");
        var probe = new DeviceProbe
        {
            DeviceId = device.Id,
            Timestamp = snap.LastChecked,
            IsOnline = snap.IsOnline,
            ConnectLatencyMs = snap.ConnectLatencyMs,
            TotalLatencyMs = snap.TotalLatencyMs,
            PlaybackServiceStatus = snap.PlaybackServiceStatus,
            PlaylistUploadServiceStatus = snap.PlaylistUploadServiceStatus,
            VideoUploadServiceStatus = snap.VideoUploadServiceStatus
        };
        return (snap, probe);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("DeviceMonitoringService started.");
        var rnd = new Random();
        var nextPoll = new ConcurrentDictionary<int, DateTime>();
        var nextPersist = new ConcurrentDictionary<int, DateTime>();
        var semaphore = new SemaphoreSlim(_settings.MaxParallelProbes);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var now = DateTime.UtcNow;
                var hasSubscribers = !_subscribers.IsEmpty;
                var fastInterval = TimeSpan.FromSeconds(Math.Max(1, _settings.ActiveSubscriberPollingIntervalSeconds));
                var lazyInterval = TimeSpan.FromSeconds(Math.Max(1, _settings.LazyPollingIntervalSeconds));

                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var devices = await db.Devices.AsNoTracking().ToListAsync(stoppingToken);

                _logger.LogDebug("Loaded {DeviceCount} devices for monitoring. Subscribers={SubscriberCount}", devices.Count, _subscribers.Count);

                foreach (var d in devices)
                {
                    nextPoll.TryAdd(d.Id, now);
                    nextPersist.TryAdd(d.Id, now);
                }

                var due = new List<Device>();
                foreach (var kvp in nextPoll.Where(kvp => kvp.Value <= now || (hasSubscribers && kvp.Value - now > fastInterval)).ToList())
                {
                    var device = devices.FirstOrDefault(d => d.Id == kvp.Key);
                    if (device is null)
                    {
                        nextPoll.TryRemove(kvp.Key, out _);
                        nextPersist.TryRemove(kvp.Key, out _);
                        continue;
                    }
                    due.Add(device);
                }

                var probeResults = new ConcurrentBag<DeviceProbe>();

                var tasks = due.Select(async device =>
                {
                    await semaphore.WaitAsync(stoppingToken);
                    try
                    {
                        var (snap, probe) = await ProbeDevice(device, stoppingToken);
                        var hasSubscribersForDevice = !_subscribers.IsEmpty;
                        var baseInterval = hasSubscribersForDevice
                            ? _settings.ActiveSubscriberPollingIntervalSeconds
                            : _settings.LazyPollingIntervalSeconds;

                        nextPoll[device.Id] = DateTime.UtcNow
                            + TimeSpan.FromSeconds(Math.Max(1, baseInterval))
                            + TimeSpan.FromSeconds(rnd.NextDouble() * _settings.JitterSeconds);

                        var persistNow = DateTime.UtcNow;
                        if (!nextPersist.TryGetValue(device.Id, out var nextPersistAt) || persistNow >= nextPersistAt)
                        {
                            probeResults.Add(probe);
                            nextPersist[device.Id] = persistNow + lazyInterval;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error probing device {DeviceId} ({IpAddress})", device.Id, device.IpAddress);
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }).ToList();

                await Task.WhenAll(tasks);

                if (!probeResults.IsEmpty)
                {
                    foreach (var probe in probeResults)
                        db.DeviceProbes.Add(probe);

                    await db.SaveChangesAsync(stoppingToken);
                    _logger.LogDebug("Saved {ProbeCount} device probe results to database.", probeResults.Count);
                }

                var next = nextPoll.Values.DefaultIfEmpty(DateTime.UtcNow.AddSeconds(_settings.FallbackIntervalSeconds)).Min();
                var delay = next - DateTime.UtcNow;
                if (delay < TimeSpan.Zero) delay = TimeSpan.Zero;
                delay = hasSubscribers && delay > fastInterval ? fastInterval : delay;
                delay = !hasSubscribers && delay > lazyInterval ? lazyInterval : delay;

                await _pollWakeSignal.WaitAsync(delay, stoppingToken);
                // Drain any extra wake signals accumulated during bursts of subscribe/unsubscribe events;
                // otherwise leftover semaphore count can cause repeated immediate wake-ups in next iterations.
                while (_pollWakeSignal.Wait(0))
                {
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "DeviceMonitoringService encountered a fatal error and is stopping.");
        }
        finally
        {
            _logger.LogInformation("DeviceMonitoringService stopped.");
        }
    }

    private async Task<DeviceProbeResult> Probe(Device device, CancellationToken token)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            if (!IPAddress.TryParse(device.IpAddress, out var ipAddress))
            {
                sw.Stop();
                _logger.LogWarning("Probe skipped: Invalid IP address '{IpAddress}'", device.IpAddress);
                return new DeviceProbeResult(false, sw.ElapsedMilliseconds, sw.ElapsedMilliseconds, null, null);
            }

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
            cts.CancelAfter(TimeSpan.FromSeconds(_settings.TimeoutSeconds));

            var connectWatch = Stopwatch.StartNew();
            var healthResponse = await _agentClient.CheckHealthAsync(device, cts.Token).ConfigureAwait(false);
            connectWatch.Stop();
            sw.Stop();

            var connectMs = connectWatch.ElapsedMilliseconds;

            if (!healthResponse.Ok)
            {
                _logger.LogDebug("Health probe for device {DeviceId} ({IpAddress}) returned error: {Error}", 
                    device.Id, device.IpAddress, healthResponse.ErrMsg);
                return new DeviceProbeResult(false, connectMs, sw.ElapsedMilliseconds, null, healthResponse.ServiceStatus);
            }

            return new DeviceProbeResult(true, connectMs, sw.ElapsedMilliseconds, healthResponse.Version, healthResponse.ServiceStatus);
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested)
        {
            sw.Stop();
            _logger.LogWarning("Health probe for device {DeviceId} ({IpAddress}) timed out.", device.Id, device.IpAddress);
            return new DeviceProbeResult(false, sw.ElapsedMilliseconds, sw.ElapsedMilliseconds, null, null);
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogWarning(ex, "Health probe failed for device {DeviceId} ({IpAddress}).", device.Id, device.IpAddress);
            return new DeviceProbeResult(false, sw.ElapsedMilliseconds, sw.ElapsedMilliseconds, null, null);
        }
    }
}

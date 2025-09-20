// Copyright (c) 2025 sw.consulting
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

        foreach (var kvp in _snapshot)
        {
            channel.Writer.TryWrite(new DeviceStatusEvent(kvp.Key, kvp.Value));
        }

        token.Register(() =>
        {
            _subscribers.TryRemove(id, out _);
            channel.Writer.TryComplete();
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
                TotalLatencyMs = 0
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
        var (IsOnline, ConnectMs, TotalMs) = await Probe(device, token);
        var snap = new DeviceStatusSnapshot
        {
            IpAddress = device.IpAddress,
            IsOnline = IsOnline,
            LastChecked = DateTime.UtcNow,
            ConnectLatencyMs = ConnectMs,
            TotalLatencyMs = TotalMs
        };
        _snapshot[device.Id] = snap;
        Broadcast(new DeviceStatusEvent(device.Id, snap));
        _logger.LogInformation("Probed device {DeviceId} ({IpAddress}): Online={IsOnline}, ConnectMs={ConnectMs}, TotalMs={TotalMs}",
            device.Id, device.IpAddress, snap.IsOnline, snap.ConnectLatencyMs, snap.TotalLatencyMs);
        var probe = new DeviceProbe
        {
            DeviceId = device.Id,
            Timestamp = snap.LastChecked,
            IsOnline = snap.IsOnline,
            ConnectLatencyMs = snap.ConnectLatencyMs,
            TotalLatencyMs = snap.TotalLatencyMs
        };
        return (snap, probe);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("DeviceMonitoringService started.");
        var rnd = new Random();
        var nextPoll = new ConcurrentDictionary<int, DateTime>();
        var semaphore = new SemaphoreSlim(_settings.MaxParallelProbes);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var devices = await db.Devices.AsNoTracking().ToListAsync(stoppingToken);

                _logger.LogDebug("Loaded {DeviceCount} devices for monitoring.", devices.Count);

                foreach (var d in devices)
                {
                    nextPoll.TryAdd(d.Id, DateTime.UtcNow);
                }

                var due = new List<Device>();
                foreach (var kvp in nextPoll.Where(kvp => kvp.Value <= DateTime.UtcNow).ToList())
                {
                    var device = devices.FirstOrDefault(d => d.Id == kvp.Key);
                    if (device is null)
                    {
                        nextPoll.TryRemove(kvp.Key, out _);
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
                        nextPoll[device.Id] = DateTime.UtcNow
                            + TimeSpan.FromSeconds(snap.IsOnline ? _settings.OnlinePollingIntervalSeconds : _settings.OfflinePollingIntervalSeconds)
                            + TimeSpan.FromSeconds(rnd.NextDouble() * _settings.JitterSeconds);
                        probeResults.Add(probe);
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

                foreach (var probe in probeResults)
                    db.DeviceProbes.Add(probe);

                try
                {
                    await db.SaveChangesAsync(stoppingToken);
                    _logger.LogDebug("Saved {ProbeCount} device probe results to database.", probeResults.Count);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error saving device probe results to database.");
                }

                var next = nextPoll.Values.DefaultIfEmpty(DateTime.UtcNow.AddSeconds(_settings.FallbackIntervalSeconds)).Min();
                var delay = next - DateTime.UtcNow;
                if (delay < TimeSpan.Zero) delay = TimeSpan.Zero;
                await Task.Delay(delay, stoppingToken);
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

    private async Task<(bool IsOnline, long ConnectMs, long TotalMs)> Probe(Device device, CancellationToken token)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            if (!IPAddress.TryParse(device.IpAddress, out var ipAddress))
            {
                sw.Stop();
                _logger.LogWarning("Probe skipped: Invalid IP address '{IpAddress}'", device.IpAddress);
                return (false, sw.ElapsedMilliseconds, sw.ElapsedMilliseconds);
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
                    device.Id, device.IpAddress, healthResponse.Error);
                return (false, connectMs, sw.ElapsedMilliseconds);
            }

            return (true, connectMs, sw.ElapsedMilliseconds);
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested)
        {
            sw.Stop();
            _logger.LogWarning("Health probe for device {DeviceId} ({IpAddress}) timed out.", device.Id, device.IpAddress);
            return (false, sw.ElapsedMilliseconds, sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogWarning(ex, "Health probe failed for device {DeviceId} ({IpAddress}).", device.Id, device.IpAddress);
            return (false, sw.ElapsedMilliseconds, sw.ElapsedMilliseconds);
        }
    }
}

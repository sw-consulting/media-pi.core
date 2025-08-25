// Copyright (c) 2025 Maxim [maxirmx] Samsonov (www.sw.consulting)
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.
//
// This file is a part of Media Pi backend application

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using MediaPi.Core.Data;
using MediaPi.Core.Models;
using MediaPi.Core.Settings;
using MediaPi.Core.Services.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MediaPi.Core.Services;

public class DeviceMonitoringService : BackgroundService, IDeviceMonitoringService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly DeviceMonitorSettings _settings;
    private readonly ConcurrentDictionary<int, DeviceStatusSnapshot> _snapshot = new();
    private readonly ILogger<DeviceMonitoringService> _logger;
    private readonly DeviceEventsService _deviceEventsService;

    public DeviceMonitoringService(
        IServiceScopeFactory scopeFactory, 
        IOptions<DeviceMonitorSettings> options, 
        ILogger<DeviceMonitoringService> logger, 
        DeviceEventsService deviceEventsService)
    {
        _scopeFactory = scopeFactory;
        _settings = options.Value;
        _logger = logger;
        _deviceEventsService = deviceEventsService;
        _deviceEventsService.DeviceDeleted += OnDeviceDeleted;
    }

    public IReadOnlyDictionary<int, DeviceStatusSnapshot> Snapshot => _snapshot;

    public bool TryGetStatus(int deviceId, out DeviceStatusSnapshot status) => _snapshot.TryGetValue(deviceId, out status!);

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
        }
    }

    private async Task<(DeviceStatusSnapshot Snapshot, DeviceProbe Probe)> ProbeDevice(Device device, CancellationToken token)
    {
        var (IsOnline, ConnectMs, TotalMs) = await Probe(device.IpAddress, token);
        var snap = new DeviceStatusSnapshot
        {
            IpAddress = device.IpAddress,
            IsOnline = IsOnline,
            LastChecked = DateTime.UtcNow,
            ConnectLatencyMs = ConnectMs,
            TotalLatencyMs = TotalMs
        };
        _snapshot[device.Id] = snap;
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

                var due = nextPoll
                    .Where(kvp => kvp.Value <= DateTime.UtcNow)
                    .Select(kvp => devices.First(d => d.Id == kvp.Key))
                    .ToList();

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

                var next = nextPoll.Values.DefaultIfEmpty(DateTime.UtcNow.AddSeconds(1)).Min();
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

    private async Task<(bool IsOnline, long ConnectMs, long TotalMs)> Probe(string ip, CancellationToken token)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            if (!IPAddress.TryParse(ip, out var ipAddress))
            {
                sw.Stop();
                _logger.LogWarning("Probe skipped: Invalid IP address '{IpAddress}'", ip);
                return (false, sw.ElapsedMilliseconds, sw.ElapsedMilliseconds);
            }
            using var client = new TcpClient();
            var connectTask = client.ConnectAsync(ipAddress, 22);
            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(_settings.TimeoutSeconds), token);
            var completed = await Task.WhenAny(connectTask, timeoutTask);
            if (completed != connectTask || !client.Connected)
            {
                sw.Stop();
                return (false, sw.ElapsedMilliseconds, sw.ElapsedMilliseconds);
            }
            var connectMs = sw.ElapsedMilliseconds;
            using var stream = client.GetStream();
            stream.ReadTimeout = _settings.TimeoutSeconds * 1000;
            var buffer = new byte[256];
            int bytesRead = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), token);
            sw.Stop();
            if (bytesRead == 0)
            {
                _logger.LogWarning("Probe for IP {IpAddress} succeeded in connecting, but no data was read from the stream.", ip);
                return (false, connectMs, sw.ElapsedMilliseconds);
            }
            if (bytesRead < buffer.Length)
            {
                _logger.LogDebug("Probe for IP {IpAddress} read partial data: {BytesRead} bytes.", ip, bytesRead);
            }
            return (true, connectMs, sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogWarning(ex, "Probe failed for IP {IpAddress}", ip);
            return (false, sw.ElapsedMilliseconds, sw.ElapsedMilliseconds);
        }
    }
}

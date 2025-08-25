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

    public DeviceMonitoringService(IServiceScopeFactory scopeFactory, IOptions<DeviceMonitorSettings> options)
    {
        _scopeFactory = scopeFactory;
        _settings = options.Value;
    }

    public IReadOnlyDictionary<int, DeviceStatusSnapshot> Snapshot => _snapshot;

    public bool TryGetStatus(int deviceId, out DeviceStatusSnapshot status) => _snapshot.TryGetValue(deviceId, out status!);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var rnd = new Random();
        var nextPoll = new ConcurrentDictionary<int, DateTime>();
        var semaphore = new SemaphoreSlim(_settings.MaxParallelProbes);

        while (!stoppingToken.IsCancellationRequested)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var devices = await db.Devices.AsNoTracking().ToListAsync(stoppingToken);

            foreach (var d in devices)
            {
                nextPoll.TryAdd(d.Id, DateTime.UtcNow);
            }
            foreach (var id in nextPoll.Keys)
            {
                if (devices.All(d => d.Id != id))
                {
                    nextPoll.TryRemove(id, out _);
                    _snapshot.TryRemove(id, out _);
                }
            }

            var due = nextPoll
                .Where(kvp => kvp.Value <= DateTime.UtcNow)
                .Select(kvp => devices.First(d => d.Id == kvp.Key))
                .ToList();

            var tasks = due.Select(async device =>
            {
                await semaphore.WaitAsync(stoppingToken);
                try
                {
                    var result = await Probe(device.IpAddress, stoppingToken);
                    var snap = new DeviceStatusSnapshot
                    {
                        IpAddress = device.IpAddress,
                        IsOnline = result.IsOnline,
                        LastChecked = DateTime.UtcNow,
                        ConnectLatencyMs = result.ConnectMs,
                        TotalLatencyMs = result.TotalMs
                    };
                    _snapshot[device.Id] = snap;
                    nextPoll[device.Id] = DateTime.UtcNow
                        + TimeSpan.FromSeconds(result.IsOnline ? _settings.OnlinePollingIntervalSeconds : _settings.OfflinePollingIntervalSeconds)
                        + TimeSpan.FromSeconds(rnd.NextDouble() * _settings.JitterSeconds);

                    db.DeviceProbes.Add(new DeviceProbe
                    {
                        DeviceId = device.Id,
                        Timestamp = snap.LastChecked,
                        IsOnline = snap.IsOnline,
                        ConnectLatencyMs = snap.ConnectLatencyMs,
                        TotalLatencyMs = snap.TotalLatencyMs
                    });
                }
                finally
                {
                    semaphore.Release();
                }
            }).ToList();

            await Task.WhenAll(tasks);
            await db.SaveChangesAsync(stoppingToken);

            var next = nextPoll.Values.DefaultIfEmpty(DateTime.UtcNow.AddSeconds(1)).Min();
            var delay = next - DateTime.UtcNow;
            if (delay < TimeSpan.Zero) delay = TimeSpan.Zero;
            await Task.Delay(delay, stoppingToken);
        }
    }

    private async Task<(bool IsOnline, long ConnectMs, long TotalMs)> Probe(string ip, CancellationToken token)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            using var client = new TcpClient();
            var connectTask = client.ConnectAsync(IPAddress.Parse(ip), 22);
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
            await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), token);
            sw.Stop();
            return (true, connectMs, sw.ElapsedMilliseconds);
        }
        catch
        {
            sw.Stop();
            return (false, sw.ElapsedMilliseconds, sw.ElapsedMilliseconds);
        }
    }
}

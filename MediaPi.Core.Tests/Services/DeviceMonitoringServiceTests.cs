// Copyright (C) 2025-2026 sw.consulting
// This file is a part of Media Pi backend

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using MediaPi.Core.Data;
using MediaPi.Core.Models;
using MediaPi.Core.Services;
using MediaPi.Core.Settings;
using MediaPi.Core.Services.Models;
using MediaPi.Core.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using NUnit.Framework;

namespace MediaPi.Core.Tests.Services;

[TestFixture]
public class DeviceMonitoringServiceTests
{
    private DeviceMonitorSettings GetDefaultSettings() => new DeviceMonitorSettings
    {
        OnlinePollingIntervalSeconds = 1,
        OfflinePollingIntervalSeconds = 2,
        MaxParallelProbes = 2,
        TimeoutSeconds = 1,
        JitterSeconds = 0
    };

    private static AppDbContext CreateDbContext(params Device[] devices)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new AppDbContext(options);
        db.Devices.AddRange(devices);
        db.SaveChanges();
        return db;
    }

    private static IServiceScopeFactory CreateScopeFactory(AppDbContext db)
    {
        var serviceProvider = new ServiceCollection()
            .AddSingleton(db)
            .BuildServiceProvider();
        var scopeFactory = new Mock<IServiceScopeFactory>();
        var scope = new Mock<IServiceScope>();
        scope.Setup(s => s.ServiceProvider).Returns(serviceProvider);
        scopeFactory.Setup(f => f.CreateScope()).Returns(scope.Object);
        return scopeFactory.Object;
    }

    private static ILogger<DeviceMonitoringService> CreateLogger(List<string> logs)
    {
        var logger = new Mock<ILogger<DeviceMonitoringService>>();
        logs ??= new List<string>();
        
        // Use a lock to handle concurrent access to the logs list
        var lockObj = new object();
        
        logger.Setup(l => l.Log(
            It.IsAny<LogLevel>(),
            It.IsAny<EventId>(),
            It.IsAny<It.IsAnyType>(),
            It.IsAny<Exception>(),
            (Func<It.IsAnyType, Exception?, string>)It.IsAny<object>()))
            .Callback((LogLevel level, EventId id, object state, Exception ex, Delegate formatter) =>
            {
                lock (lockObj)
                {
                    logs.Add($"{level}: {state}");
                }
            });
        return logger.Object;
    }

    private static DeviceEventsService CreateDeviceEventsService()
    {
        return new DeviceEventsService();
    }

    private static IMediaPiAgentClient CreateAgentClient(bool isHealthy = true, string? error = null, string? version = "1.0.0")
    {
        var mock = new Mock<IMediaPiAgentClient>();
        
        mock.Setup(c => c.CheckHealthAsync(It.IsAny<Device>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MediaPiAgentHealthResponse
            {
                Ok = isHealthy,
                ErrMsg = error,
                Status = isHealthy ? "healthy" : "unhealthy",
                Uptime = 12345.67,
                Version = version
            });

        return mock.Object;
    }

    [Test]
    public void TryGetStatus_ReturnsFalse_WhenNotPresent()
    {
        var db = CreateDbContext();
        var logs = new List<string>();
        var service = new DeviceMonitoringService(
            CreateScopeFactory(db),
            Options.Create(GetDefaultSettings()),
            CreateLogger(logs),
            CreateDeviceEventsService(),
            CreateAgentClient());
        // With
        Assert.That(service.Snapshot, Is.Not.Null);
        Assert.That(service.TryGetStatus(123, out var _), Is.False);
    }

    [Test]
    public async Task ExecuteAsync_UpdatesSnapshot_ForOnlineDevice()
    {
        var device = new Device { Id = 1, IpAddress = "127.0.0.1", Port = 8080, Name = "TestDevice1" };
        var db = CreateDbContext(device);
        var logs = new List<string>();
        var service = new DeviceMonitoringService(
            CreateScopeFactory(db),
            Options.Create(GetDefaultSettings()),
            CreateLogger(logs),
            CreateDeviceEventsService(),
            CreateAgentClient(isHealthy: true, version: "2.1.0"));

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var task = service.StartAsync(cts.Token);
        await Task.Delay(1500);
        cts.Cancel();
        await task;

        Assert.That(service.Snapshot.ContainsKey(device.Id), Is.True);
        var snapshot = service.Snapshot[device.Id];
        Assert.That(snapshot.SoftwareVersion, Is.EqualTo("2.1.0"));
        
        // Use thread-safe check for logs with a small delay to allow pending log writes
        await Task.Delay(100);
        bool hasProbeLog;
        lock (logs)
        {
            hasProbeLog = logs.Any(l => l.Contains("Probed device") && l.Contains("Version=2.1.0"));
        }
        Assert.That(hasProbeLog, Is.True);
    }

    [Test]
    public async Task ExecuteAsync_UpdatesSnapshot_ForOfflineDevice()
    {
        var device = new Device { Id = 2, IpAddress = "127.0.0.1", Port = 8080, Name = "TestDevice2" };
        var db = CreateDbContext(device);
        var logs = new List<string>();
        var service = new DeviceMonitoringService(
            CreateScopeFactory(db),
            Options.Create(GetDefaultSettings()),
            CreateLogger(logs),
            CreateDeviceEventsService(),
            CreateAgentClient(isHealthy: false, error: "Service unavailable", version: null));

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var task = service.StartAsync(cts.Token);
        await Task.Delay(1500);
        cts.Cancel();
        await task;

        Assert.That(service.Snapshot.ContainsKey(device.Id), Is.True);
        var snapshot = service.Snapshot[device.Id];
        Assert.That(snapshot.IsOnline, Is.False);
        Assert.That(snapshot.SoftwareVersion, Is.Null);
    }

    [Test]
    public async Task ExecuteAsync_RemovesDevice_WhenDeleted()
    {
        var device = new Device { Id = 3, IpAddress = "127.0.0.1", Port = 8080, Name = "TestDevice3" };
        var db = CreateDbContext(device);
        var logs = new List<string>();
        var eventsService = CreateDeviceEventsService();
        var service = new DeviceMonitoringService(
            CreateScopeFactory(db),
            Options.Create(GetDefaultSettings()),
            CreateLogger(logs),
            eventsService,
            CreateAgentClient());

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        var task = service.StartAsync(cts.Token);
        
        // Wait for the device to be added to snapshot first
        await Task.Delay(1500);
        
        // Verify device is in snapshot before deletion
        Assert.That(service.Snapshot.ContainsKey(device.Id), Is.True, "Device should be in snapshot before deletion");

        // Use events service to simulate device deletion
        eventsService.OnDeviceDeleted(device.Id);

        await Task.Delay(100); // Brief delay for event processing
        cts.Cancel();
        await task;

        Assert.That(service.Snapshot.ContainsKey(device.Id), Is.False);
    }

    [Test]
    public async Task Probe_ReturnsFalse_ForInvalidIp()
    {
        var db = CreateDbContext();
        var logs = new List<string>();
        var service = new DeviceMonitoringService(
            CreateScopeFactory(db),
            Options.Create(GetDefaultSettings()),
            CreateLogger(logs),
            CreateDeviceEventsService(),
            CreateAgentClient());
        var method = typeof(DeviceMonitoringService).GetMethod("Probe", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.That(method, Is.Not.Null); // Ensure method is found to avoid null dereference
        var task = method?.Invoke(service, new object[] { new Device { Id = 99, IpAddress = "invalid_ip", Port = 8080, Name = "Invalid" }, CancellationToken.None }) as Task<DeviceProbeResult>;
        Assert.That(task, Is.Not.Null); // Ensure task is not null to avoid null dereference
        var result = await task!;
        Assert.That(result.IsOnline, Is.False);
        Assert.That(result.SoftwareVersion, Is.Null); // Software version should be null for invalid IP
    }

    [Test]
    public async Task Probe_ReturnsCorrectSoftwareVersion_ForHealthyDevice()
    {
        var device = new Device { Id = 13, IpAddress = "127.0.0.1", Port = 8080, Name = "TestDevice13" };
        var db = CreateDbContext(device);
        var service = new DeviceMonitoringService(
            CreateScopeFactory(db),
            Options.Create(GetDefaultSettings()),
            CreateLogger(new List<string>()),
            CreateDeviceEventsService(),
            CreateAgentClient(isHealthy: true, version: "test-version-1.2.3"));

        var method = typeof(DeviceMonitoringService).GetMethod("Probe", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.That(method, Is.Not.Null);
        
        var task = method?.Invoke(service, new object[] { device, CancellationToken.None }) as Task<DeviceProbeResult>;
        Assert.That(task, Is.Not.Null);
        
        var result = await task!;
        Assert.That(result.IsOnline, Is.True); // IsOnline should be true
        Assert.That(result.SoftwareVersion, Is.EqualTo("test-version-1.2.3")); // Software version should match
    }

    [Test]
    public async Task Probe_ReturnsNullSoftwareVersion_ForUnhealthyDevice()
    {
        var device = new Device { Id = 14, IpAddress = "127.0.0.1", Port = 8080, Name = "TestDevice14" };
        var db = CreateDbContext(device);
        var service = new DeviceMonitoringService(
            CreateScopeFactory(db),
            Options.Create(GetDefaultSettings()),
            CreateLogger(new List<string>()),
            CreateDeviceEventsService(),
            CreateAgentClient(isHealthy: false, error: "Service down", version: null));

        var method = typeof(DeviceMonitoringService).GetMethod("Probe", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.That(method, Is.Not.Null);
        
        var task = method?.Invoke(service, new object[] { device, CancellationToken.None }) as Task<DeviceProbeResult>;
        Assert.That(task, Is.Not.Null);
        
        var result = await task!;
        Assert.That(result.IsOnline, Is.False); // IsOnline should be false
        Assert.That(result.SoftwareVersion, Is.Null); // Software version should be null for unhealthy device
    }

    [Test]
    public async Task ExecuteAsync_SavesDeviceProbes()
    {
        var device = new Device { Id = 4, IpAddress = "127.0.0.1", Port = 8080, Name = "TestDevice4" };
        var db = CreateDbContext(device);
        var logs = new List<string>();
        var service = new DeviceMonitoringService(
            CreateScopeFactory(db),
            Options.Create(GetDefaultSettings()),
            CreateLogger(logs),
            CreateDeviceEventsService(),
            CreateAgentClient());

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var task = service.StartAsync(cts.Token);
        await Task.Delay(1500);
        cts.Cancel();
        await task;

        var probes = db.DeviceProbes.Where(p => p.DeviceId == device.Id).ToList();
        Assert.That(probes, Is.Not.Empty);
    }

    [Test]
    public async Task Test_ReturnsSnapshot_WhenDeviceExists()
    {
        var device = new Device { Id = 5, IpAddress = "127.0.0.1", Port = 8080, Name = "TestDevice5" };
        var db = CreateDbContext(device);
        var service = new DeviceMonitoringService(
            CreateScopeFactory(db),
            Options.Create(GetDefaultSettings()),
            CreateLogger(new List<string>()),
            CreateDeviceEventsService(),
            CreateAgentClient(version: "3.2.1"));

        var result = await service.Test(device.Id);

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.SoftwareVersion, Is.EqualTo("3.2.1"));
        Assert.That(service.Snapshot.ContainsKey(device.Id), Is.True);
        Assert.That(service.Snapshot[device.Id].SoftwareVersion, Is.EqualTo("3.2.1"));
    }

    [Test]
    public async Task Test_ReturnsNull_WhenDeviceMissing()
    {
        var db = CreateDbContext();
        var service = new DeviceMonitoringService(
            CreateScopeFactory(db),
            Options.Create(GetDefaultSettings()),
            CreateLogger(new List<string>()),
            CreateDeviceEventsService(),
            CreateAgentClient());

        var result = await service.Test(999);

        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task Subscribe_ReceivesUpdates()
    {
        var device = new Device { Id = 6, IpAddress = "127.0.0.1", Port = 8080, Name = "TestDevice6" };
        var db = CreateDbContext(device);
        var service = new DeviceMonitoringService(
            CreateScopeFactory(db),
            Options.Create(GetDefaultSettings()),
            CreateLogger(new List<string>()),
            CreateDeviceEventsService(),
            CreateAgentClient());

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await using var enumerator = service.Subscribe(cts.Token).GetAsyncEnumerator(cts.Token);
        var moveNextTask = enumerator.MoveNextAsync().AsTask();

        _ = service.Test(device.Id, cts.Token);

        Assert.That(await moveNextTask, Is.True);
        var update = enumerator.Current;
        Assert.That(update.DeviceId, Is.EqualTo(device.Id));
    }

    [Test]
    public async Task Subscribe_SendsExistingSnapshot()
    {
        var device = new Device { Id = 7, IpAddress = "127.0.0.1", Port = 8080, Name = "TestDevice7" };
        var db = CreateDbContext(device);
        var service = new DeviceMonitoringService(
            CreateScopeFactory(db),
            Options.Create(GetDefaultSettings()),
            CreateLogger(new List<string>()),
            CreateDeviceEventsService(),
            CreateAgentClient());

        // Test device to populate snapshot
        await service.Test(device.Id);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await using var enumerator = service.Subscribe(cts.Token).GetAsyncEnumerator(cts.Token);

        Assert.That(await enumerator.MoveNextAsync(), Is.True);
        var update = enumerator.Current;
        Assert.That(update.DeviceId, Is.EqualTo(device.Id));
    }

    [Test]
    public async Task Test_HandlesAgentClientException()
    {
        var device = new Device { Id = 8, IpAddress = "127.0.0.1", Port = 8080, Name = "TestDevice8" };
        var db = CreateDbContext(device);
        var logs = new List<string>();

        var agentMock = new Mock<IMediaPiAgentClient>();
        agentMock.Setup(c => c.CheckHealthAsync(It.IsAny<Device>(), It.IsAny<CancellationToken>()))
                 .ThrowsAsync(new InvalidOperationException("Connection failed"));

        var service = new DeviceMonitoringService(
            CreateScopeFactory(db),
            Options.Create(GetDefaultSettings()),
            CreateLogger(logs),
            CreateDeviceEventsService(),
            agentMock.Object);

        var result = await service.Test(device.Id);

        Assert.That(result, Is.Not.Null);
        Assert.That(result?.IsOnline, Is.False);
        Assert.That(result?.SoftwareVersion, Is.Null);
        
        // Check if warning was logged
        await Task.Delay(100);
        bool hasWarningLog;
        lock (logs)
        {
            hasWarningLog = logs.Any(l => l.Contains("Warning") && l.Contains("Health probe failed"));
        }
        Assert.That(hasWarningLog, Is.True);
    }

    [Test]
    public async Task TryGetStatusItem_ReturnsSoftwareVersion_WhenDeviceInSnapshot()
    {
        var device = new Device { Id = 9, IpAddress = "127.0.0.1", Port = 8080, Name = "TestDevice9" };
        var db = CreateDbContext(device);
        var service = new DeviceMonitoringService(
            CreateScopeFactory(db),
            Options.Create(GetDefaultSettings()),
            CreateLogger(new List<string>()),
            CreateDeviceEventsService(),
            CreateAgentClient(version: "4.0.0"));

        // First test the device to populate snapshot
        await service.Test(device.Id);

        // Now test TryGetStatusItem
        var success = service.TryGetStatusItem(device.Id, out var statusItem);

        Assert.That(success, Is.True);
        Assert.That(statusItem, Is.Not.Null);
        Assert.That(statusItem!.DeviceId, Is.EqualTo(device.Id));
        Assert.That(statusItem.SoftwareVersion, Is.EqualTo("4.0.0"));
        Assert.That(statusItem.IsOnline, Is.True);
    }

    [Test]
    public async Task Probe_HandlesTimeoutCorrectly()
    {
        var device = new Device { Id = 10, IpAddress = "127.0.0.1", Port = 8080, Name = "TestDevice10" };
        var db = CreateDbContext(device);
        var logs = new List<string>();

        var agentMock = new Mock<IMediaPiAgentClient>();
        agentMock.Setup(c => c.CheckHealthAsync(It.IsAny<Device>(), It.IsAny<CancellationToken>()))
                 .ThrowsAsync(new OperationCanceledException("Request timed out"));

        var service = new DeviceMonitoringService(
            CreateScopeFactory(db),
            Options.Create(GetDefaultSettings()),
            CreateLogger(logs),
            CreateDeviceEventsService(),
            agentMock.Object);

        var result = await service.Test(device.Id);

        Assert.That(result, Is.Not.Null);
        Assert.That(result?.IsOnline, Is.False);
        Assert.That(result?.SoftwareVersion, Is.Null);
    }

    [Test]
    public async Task DeviceStatusSnapshot_ContainsSoftwareVersionAfterDeletion()
    {
        var device = new Device { Id = 11, IpAddress = "127.0.0.1", Port = 8080, Name = "TestDevice11" };
        var db = CreateDbContext(device);
        var eventsService = CreateDeviceEventsService();
        var events = new List<DeviceStatusEvent>();
        
        var service = new DeviceMonitoringService(
            CreateScopeFactory(db),
            Options.Create(GetDefaultSettings()),
            CreateLogger(new List<string>()),
            eventsService,
            CreateAgentClient(version: "5.0.0"));

        // Subscribe to events to capture deletion event
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var subscriptionTask = Task.Run(async () =>
        {
            await foreach (var evt in service.Subscribe(cts.Token))
            {
                events.Add(evt);
                if (!evt.Snapshot.IsOnline && string.IsNullOrEmpty(evt.Snapshot.IpAddress))
                {
                    // This is the deletion event
                    break;
                }
            }
        });

        // First test the device to populate snapshot
        await service.Test(device.Id);
        
        // Wait a bit for the event to be captured
        await Task.Delay(100);

        // Trigger deletion
        eventsService.OnDeviceDeleted(device.Id);

        // Wait for the subscription task to complete or timeout
        try
        {
            await subscriptionTask.WaitAsync(TimeSpan.FromSeconds(2));
        }
        catch (TimeoutException) { }

        cts.Cancel();
        
        // Verify device was removed from snapshot
        Assert.That(service.Snapshot.ContainsKey(device.Id), Is.False);
        
        // Verify deletion event was sent with proper structure
        var deletionEvent = events.LastOrDefault(e => e.DeviceId == device.Id && !e.Snapshot.IsOnline);
        Assert.That(deletionEvent, Is.Not.Null);
        if (deletionEvent is not null)
        {
            Assert.That(deletionEvent.Snapshot.SoftwareVersion, Is.Null);
            Assert.That(deletionEvent.Snapshot.IpAddress, Is.EqualTo(string.Empty));
        }
    }

    [Test]
    public async Task Subscribe_SendsExistingSnapshotWithSoftwareVersion()
    {
        var device = new Device { Id = 12, IpAddress = "127.0.0.1", Port = 8080, Name = "TestDevice12" };
        var db = CreateDbContext(device);
        var service = new DeviceMonitoringService(
            CreateScopeFactory(db),
            Options.Create(GetDefaultSettings()),
            CreateLogger(new List<string>()),
            CreateDeviceEventsService(),
            CreateAgentClient(version: "6.1.2"));

        // Test device to populate snapshot
        await service.Test(device.Id);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await using var enumerator = service.Subscribe(cts.Token).GetAsyncEnumerator(cts.Token);

        Assert.That(await enumerator.MoveNextAsync(), Is.True);
        var update = enumerator.Current;
        Assert.That(update.DeviceId, Is.EqualTo(device.Id));
        Assert.That(update.Snapshot.SoftwareVersion, Is.EqualTo("6.1.2"));
        Assert.That(update.Snapshot.IsOnline, Is.True);
    }
}

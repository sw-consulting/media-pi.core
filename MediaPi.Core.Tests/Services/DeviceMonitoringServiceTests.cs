using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediaPi.Core.Data;
using MediaPi.Core.Models;
using MediaPi.Core.Services;
using MediaPi.Core.Settings;
using MediaPi.Core.Services.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
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
        logger.Setup(l => l.Log(
            It.IsAny<LogLevel>(),
            It.IsAny<EventId>(),
            It.IsAny<It.IsAnyType>(),
            It.IsAny<Exception>(),
            (Func<It.IsAnyType, Exception?, string>)It.IsAny<object>()))
            .Callback((LogLevel level, EventId id, object state, Exception ex, Delegate formatter) =>
            {
                logs.Add($"{level}: {state}");
            });
        return logger.Object;
    }

    private static DeviceEventsService CreateDeviceEventsService()
    {
        return new DeviceEventsService();
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
            CreateDeviceEventsService());
        // With
        Assert.That(service.Snapshot, Is.Not.Null);
        Assert.That(service.TryGetStatus(123, out var _), Is.False);
    }

    [Test]
    public async Task ExecuteAsync_UpdatesSnapshot_ForOnlineDevice()
    {
        var device = new Device { Id = 1, IpAddress = "127.0.0.1", Name = "TestDevice1" };
        var db = CreateDbContext(device);
        var logs = new List<string>();
        var service = new DeviceMonitoringService(
            CreateScopeFactory(db),
            Options.Create(GetDefaultSettings()),
            CreateLogger(logs),
            CreateDeviceEventsService());

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var task = service.StartAsync(cts.Token);
        await Task.Delay(1500);
        cts.Cancel();
        await task;

        Assert.That(service.Snapshot.ContainsKey(device.Id), Is.True);
        Assert.That(logs.Any(l => l.Contains("Probed device")), Is.True);
    }

    [Test]
    public async Task ExecuteAsync_RemovesDevice_WhenDeleted()
    {
        var device = new Device { Id = 2, IpAddress = "127.0.0.1", Name = "TestDevice2" };
        var db = CreateDbContext(device);
        var logs = new List<string>();
        var eventsService = CreateDeviceEventsService();
        var service = new DeviceMonitoringService(
            CreateScopeFactory(db),
            Options.Create(GetDefaultSettings()),
            CreateLogger(logs),
            eventsService);

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
            CreateDeviceEventsService());
        var method = typeof(DeviceMonitoringService).GetMethod("Probe", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.That(method, Is.Not.Null); // Ensure method is found to avoid null dereference
        var task = method?.Invoke(service, new object[] { "invalid_ip", CancellationToken.None }) as Task<(bool, long, long)>;
        Assert.That(task, Is.Not.Null); // Ensure task is not null to avoid null dereference
        var result = await task!;
        Assert.That(result.Item1, Is.False);
    }

    [Test]
    public async Task ExecuteAsync_SavesDeviceProbes()
    {
        var device = new Device { Id = 3, IpAddress = "127.0.0.1", Name = "TestDevice3" };
        var db = CreateDbContext(device);
        var logs = new List<string>();
        var service = new DeviceMonitoringService(
            CreateScopeFactory(db),
            Options.Create(GetDefaultSettings()),
            CreateLogger(logs),
            CreateDeviceEventsService());

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var task = service.StartAsync(cts.Token);
        await Task.Delay(1500);
        cts.Cancel();
        await task;

        var probes = db.DeviceProbes.Where(p => p.DeviceId == device.Id).ToList();
        Assert.That(probes, Is.Not.Empty);
    }


}

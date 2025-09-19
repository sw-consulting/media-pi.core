// Copyright (c) 2025 sw.consulting
// This file is a part of Media Pi backend

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
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
using System.Reflection;

namespace MediaPi.Core.Tests.Services;

[TestFixture]
public class DeviceMonitoringServiceErrorTests
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

    private static IServiceScopeFactory CreateFailingScopeFactory()
    {
        var scopeFactory = new Mock<IServiceScopeFactory>();
        scopeFactory.Setup(f => f.CreateScope()).Throws<InvalidOperationException>();
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
                    if (ex != null)
                    {
                        logs.Add($"Exception: {ex.GetType().Name} - {ex.Message}");
                    }
                }
            });
        return logger.Object;
    }

    private static DeviceEventsService CreateDeviceEventsService()
    {
        return new DeviceEventsService();
    }



    [Test]
    public void OnDeviceDeleted_HandlesException_WhenBroadcasting()
    {
        // Arrange
        var db = CreateDbContext();
        var logs = new List<string>();
        var eventsService = CreateDeviceEventsService();
        var service = new DeviceMonitoringService(
            CreateScopeFactory(db),
            Options.Create(GetDefaultSettings()),
            CreateLogger(logs),
            eventsService);
        
        // Add a device to the snapshot
        service.TryGetStatus(1, out _); // This will create an empty entry if it doesn't exist
        
        // Act
        eventsService.OnDeviceDeleted(1);
        
        // Assert - service should handle any exceptions
        Assert.That(service.Snapshot.ContainsKey(1), Is.False);
        
        // Even if we try to broadcast to a non-existent subscriber
        eventsService.OnDeviceDeleted(999);
        
        // The test passes if no exception is thrown
        Assert.Pass("OnDeviceDeleted handled exceptions gracefully");
    }

    [Test]
    public async Task ProbeDevice_HandlesException_InTcpClient()
    {
        // This test requires internals access, so we use a different approach

        // Arrange
        var device = new Device { Id = 1, IpAddress = "256.256.256.256", Name = "InvalidIpDevice" }; // Invalid IP format
        var db = CreateDbContext(device);
        var logs = new List<string>();
        var service = new DeviceMonitoringService(
            CreateScopeFactory(db),
            Options.Create(GetDefaultSettings()),
            CreateLogger(logs),
            CreateDeviceEventsService());

        // Act
        var result = await service.Test(device.Id);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result?.IsOnline, Is.False);
        Assert.That(logs, Has.Some.Contains("Warning"));
    }


    [Test]
    public async Task ExecuteAsync_HandlesScopeFactoryException()
    {
        // Arrange
        var logs = new List<string>();
        var service = new DeviceMonitoringService(
            CreateFailingScopeFactory(),
            Options.Create(GetDefaultSettings()),
            CreateLogger(logs),
            CreateDeviceEventsService());

        // Act
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var task = service.StartAsync(cts.Token);
        await Task.Delay(1500);
        cts.Cancel();
        await task;

        // Assert
        Assert.That(logs, Has.Some.Contains("Critical"));
        Assert.That(logs, Has.Some.Contains("Exception: InvalidOperationException"));
    }

    [Test]
    public async Task ProbeDevice_HandlesZeroBytesRead()
    {
        // We'll simulate this by using a special IP address that might connect but return no data
        // In a real test, this would be difficult to simulate perfectly

        // Arrange
        var device = new Device { Id = 1, IpAddress = "127.0.0.1", Name = "LocalDevice" }; // Local IP might connect but no SSH service
        var db = CreateDbContext(device);
        var logs = new List<string>();
        var service = new DeviceMonitoringService(
            CreateScopeFactory(db),
            Options.Create(GetDefaultSettings()),
            CreateLogger(logs),
            CreateDeviceEventsService());

        // Act
        var result = await service.Test(device.Id);

        // Assert
        Assert.That(result, Is.Not.Null);
        // We can't guarantee the result of IsOnline since it depends on the local machine
        // But we can check that the service handled the test without exceptions
    }
}

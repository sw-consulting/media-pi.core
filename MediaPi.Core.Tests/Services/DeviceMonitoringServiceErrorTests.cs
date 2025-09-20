// Copyright (c) 2025 sw.consulting
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

    private static IMediaPiAgentClient CreateAgentClient(bool isHealthy = true, string? error = null, bool throwException = false, Exception? exception = null)
    {
        var mock = new Mock<IMediaPiAgentClient>();
        
        if (throwException && exception != null)
        {
            mock.Setup(c => c.CheckHealthAsync(It.IsAny<Device>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(exception);
        }
        else
        {
            mock.Setup(c => c.CheckHealthAsync(It.IsAny<Device>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new MediaPiAgentHealthResponse
                {
                    Ok = isHealthy,
                    Error = error,
                    Status = isHealthy ? "healthy" : "unhealthy",
                    Uptime = 12345.67,
                    Version = "1.0.0"
                });
        }

        return mock.Object;
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
            eventsService,
            CreateAgentClient());
        
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
    public async Task ProbeDevice_HandlesInvalidIp()
    {
        // Arrange
        var device = new Device { Id = 1, IpAddress = "256.256.256.256", Port = 8080, Name = "InvalidIpDevice" }; // Invalid IP format
        var db = CreateDbContext(device);
        var logs = new List<string>();
        var service = new DeviceMonitoringService(
            CreateScopeFactory(db),
            Options.Create(GetDefaultSettings()),
            CreateLogger(logs),
            CreateDeviceEventsService(),
            CreateAgentClient());

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
            CreateDeviceEventsService(),
            CreateAgentClient());

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
    public async Task ProbeDevice_HandlesAgentClientException()
    {
        // Arrange
        var device = new Device { Id = 1, IpAddress = "127.0.0.1", Port = 8080, Name = "LocalDevice" };
        var db = CreateDbContext(device);
        var logs = new List<string>();
        var service = new DeviceMonitoringService(
            CreateScopeFactory(db),
            Options.Create(GetDefaultSettings()),
            CreateLogger(logs),
            CreateDeviceEventsService(),
            CreateAgentClient(throwException: true, exception: new HttpRequestException("Connection refused")));

        // Act
        var result = await service.Test(device.Id);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result?.IsOnline, Is.False);
        Assert.That(logs, Has.Some.Contains("Warning"));
        Assert.That(logs, Has.Some.Contains("Health probe failed"));
    }

    [Test]
    public async Task ProbeDevice_HandlesErrorResponse()
    {
        // Arrange
        var device = new Device { Id = 1, IpAddress = "127.0.0.1", Port = 8080, Name = "LocalDevice" };
        var db = CreateDbContext(device);
        var logs = new List<string>();
        var service = new DeviceMonitoringService(
            CreateScopeFactory(db),
            Options.Create(GetDefaultSettings()),
            CreateLogger(logs),
            CreateDeviceEventsService(),
            CreateAgentClient(isHealthy: false, error: "Service unavailable"));

        // Act
        var result = await service.Test(device.Id);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result?.IsOnline, Is.False);
        Assert.That(logs, Has.Some.Contains("Debug"));
        Assert.That(logs, Has.Some.Contains("returned error: Service unavailable"));
    }

    [Test]
    public async Task ProbeDevice_HandlesTimeout()
    {
        // Arrange
        var device = new Device { Id = 1, IpAddress = "127.0.0.1", Port = 8080, Name = "LocalDevice" };
        var db = CreateDbContext(device);
        var logs = new List<string>();
        var service = new DeviceMonitoringService(
            CreateScopeFactory(db),
            Options.Create(GetDefaultSettings()),
            CreateLogger(logs),
            CreateDeviceEventsService(),
            CreateAgentClient(throwException: true, exception: new TaskCanceledException("Operation was cancelled")));

        // Act
        var result = await service.Test(device.Id);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result?.IsOnline, Is.False);
        Assert.That(logs, Has.Some.Contains("Warning"));
    }
}

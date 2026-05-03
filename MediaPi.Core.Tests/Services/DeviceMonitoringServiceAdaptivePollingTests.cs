// Copyright (C) 2025-2026 sw.consulting
// This file is a part of Media Pi backend

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediaPi.Core.Data;
using MediaPi.Core.Models;
using MediaPi.Core.Services;
using MediaPi.Core.Services.Interfaces;
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
public class DeviceMonitoringServiceAdaptivePollingTests
{
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

    private static ILogger<DeviceMonitoringService> CreateLogger()
    {
        return Mock.Of<ILogger<DeviceMonitoringService>>();
    }

    private static DeviceEventsService CreateDeviceEventsService() => new();

    [Test]
    public async Task ExecuteAsync_UsesFastPolling_WhenSubscriberExists()
    {
        var device = new Device { Id = 101, IpAddress = "127.0.0.1", Port = 8080, Name = "AdaptiveFast" };
        var db = CreateDbContext(device);
        var calls = 0;

        var agentMock = new Mock<IMediaPiAgentClient>();
        agentMock.Setup(c => c.CheckHealthAsync(It.IsAny<Device>(), It.IsAny<CancellationToken>()))
            .Callback(() => Interlocked.Increment(ref calls))
            .ReturnsAsync(new MediaPiAgentHealthResponse { Ok = true, Version = "1.0.0" });

        var service = new DeviceMonitoringService(
            CreateScopeFactory(db),
            Options.Create(new DeviceMonitorSettings
            {
                ActiveSubscriberPollingIntervalSeconds = 1,
                LazyPollingIntervalSeconds = 3600,
                MaxParallelProbes = 1,
                TimeoutSeconds = 1,
                JitterSeconds = 0,
                FallbackIntervalSeconds = 1
            }),
            CreateLogger(),
            CreateDeviceEventsService(),
            agentMock.Object);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(4));
        var runTask = service.StartAsync(cts.Token);

        await using var e = service.Subscribe(cts.Token).GetAsyncEnumerator(cts.Token);
        _ = e.MoveNextAsync().AsTask();

        await Task.Delay(2500, cts.Token);
        cts.Cancel();
        await runTask;

        Assert.That(calls, Is.GreaterThanOrEqualTo(2));
    }
}

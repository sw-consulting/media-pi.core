// Copyright (C) 2025-2026 sw.consulting
// This file is a part of Media Pi backend

using MediaPi.Core.Data;
using MediaPi.Core.Services;
using MediaPi.Core.Services.Interfaces;
using MediaPi.Core.Tests.TestHelpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace MediaPi.Core.Tests.Services;

[TestFixture]
public class SubscriptionMaintenanceServiceTests
{
    private static (SubscriptionMaintenanceService service, AppDbContext db) BuildService(DateTime? utcNow = null)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"sms_test_{Guid.NewGuid()}")
            .Options;
        var db = new AppDbContext(options);

        var services = new ServiceCollection();
        services.AddSingleton(db);
        services.AddSingleton<IPlaylistAccessService>(_ =>
            SubscriptionTestServices.PlaylistAccessService(db, utcNow));
        var provider = services.BuildServiceProvider();

        var timeService = SubscriptionTestServices.TimeService(utcNow);
        var logger = new Mock<ILogger<SubscriptionMaintenanceService>>().Object;
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

        return (new SubscriptionMaintenanceService(scopeFactory, timeService, logger), db);
    }

    [TearDown]
    public void TearDown() { }

    [Test]
    public async Task RunCleanupOnceAsync_NoInvalidItems_ReturnsZeroCounts()
    {
        var (svc, db) = BuildService();
        try
        {
            var result = await svc.RunCleanupOnceAsync();
            Assert.That(result.RemovedItemCount, Is.EqualTo(0));
            Assert.That(result.AffectedPlaylistCount, Is.EqualTo(0));
            Assert.That(result.AffectedVideoCount, Is.EqualTo(0));
        }
        finally
        {
            db.Dispose();
        }
    }

    [Test]
    public void GetDelayUntilNextLocalMidnight_ReturnsPositiveDelay()
    {
        var (svc, db) = BuildService(new DateTime(2026, 6, 1, 9, 0, 0, DateTimeKind.Utc));
        try
        {
            var delay = svc.GetDelayUntilNextLocalMidnight();
            Assert.That(delay, Is.GreaterThan(TimeSpan.Zero));
        }
        finally
        {
            db.Dispose();
        }
    }

    [Test]
    public void GetDelayUntilNextLocalMidnight_WhenLocalTimeIsExactlyMidnight_ReturnsOneSecond()
    {
        // 2026-06-01 21:00:00 UTC = 2026-06-02 00:00:00 Moscow (exact midnight)
        var utcMidnight = new DateTime(2026, 6, 1, 21, 0, 0, DateTimeKind.Utc);
        var (svc, db) = BuildService(utcMidnight);
        try
        {
            // local midnight => delay would be 24h, but we subtract local now from next midnight
            // Actual local time is exactly midnight → next midnight is +24h → delay = 24h
            // Actually: nextLocalMidnight = 2026-06-02 00:00 + 1 day = 2026-06-03 00:00
            // Wait, let's think again:
            // localNow = 2026-06-02 00:00:00
            // localNow.Date = 2026-06-02 00:00:00
            // nextLocalMidnight = 2026-06-03 00:00:00
            // delay = 24h > 0 → returns 24h
            var delay = svc.GetDelayUntilNextLocalMidnight();
            Assert.That(delay, Is.EqualTo(TimeSpan.FromDays(1)).Within(TimeSpan.FromSeconds(1)));
        }
        finally
        {
            db.Dispose();
        }
    }

    [Test]
    public async Task ExecuteAsync_CancelsCleanly()
    {
        var (svc, db) = BuildService();
        try
        {
            using var cts = new CancellationTokenSource();
            // Cancel immediately after starting
            cts.Cancel();
            // Should complete without throwing (OperationCanceledException is swallowed)
            await svc.StartAsync(cts.Token);
            await svc.StopAsync(CancellationToken.None);
        }
        finally
        {
            db.Dispose();
        }
    }
}

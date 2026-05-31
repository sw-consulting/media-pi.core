// Copyright (C) 2025-2026 sw.consulting
// This file is a part of Media Pi backend

using MediaPi.Core.Models;
using MediaPi.Core.Services;
using MediaPi.Core.Services.Interfaces;
using MediaPi.Core.Settings;
using MediaPi.Core.Tests.TestHelpers;
using Microsoft.Extensions.Options;
using NUnit.Framework;
using System;

namespace MediaPi.Core.Tests.Services;

[TestFixture]
public class SubscriptionTimeServiceTests
{
    // UTC 2026-06-01 09:00:00 => Moscow local 2026-06-01 12:00:00 (UTC+3)
    private static readonly DateTime UtcReference = new(2026, 6, 1, 9, 0, 0, DateTimeKind.Utc);

    private static SubscriptionTimeService Build(DateTime? utcNow = null) =>
        (SubscriptionTimeService)SubscriptionTestServices.TimeService(utcNow ?? UtcReference);

    // ──────────────────────────────────────────────────────
    // UtcNow / LocalNow
    // ──────────────────────────────────────────────────────

    [Test]
    public void UtcNow_ReturnsProvidedClockValue()
    {
        var svc = Build();
        Assert.That(svc.UtcNow, Is.EqualTo(UtcReference));
        Assert.That(svc.UtcNow.Kind, Is.EqualTo(DateTimeKind.Utc));
    }

    [Test]
    public void LocalNow_ReturnsUtcPlusThreeHours()
    {
        var svc = Build();
        // Moscow is UTC+3
        var expected = UtcReference.AddHours(3);
        Assert.That(svc.LocalNow, Is.EqualTo(expected).Within(TimeSpan.FromSeconds(1)));
    }

    // ──────────────────────────────────────────────────────
    // ToUtcStart / ToUtcEnd
    // ──────────────────────────────────────────────────────

    [Test]
    public void ToUtcStart_ConvertsMidnightLocalToUtc()
    {
        var svc = Build();
        var local = new DateOnly(2026, 6, 1);
        var result = svc.ToUtcStart(local);
        // Midnight Moscow = UTC-3 = 2026-05-31 21:00:00 UTC
        Assert.That(result, Is.EqualTo(new DateTime(2026, 5, 31, 21, 0, 0, DateTimeKind.Utc)));
    }

    [Test]
    public void ToUtcEnd_ConvertsLastTickOfDayLocalToUtc()
    {
        var svc = Build();
        var local = new DateOnly(2026, 6, 1);
        var result = svc.ToUtcEnd(local);
        // 23:59:59.9999999 Moscow = day+1 midnight - 1 tick - 3h UTC
        var expected = new DateTime(2026, 6, 1, 20, 59, 59, 999, DateTimeKind.Utc)
            .AddMicroseconds(999).AddTicks(9);
        Assert.That(result.Date, Is.EqualTo(new DateTime(2026, 6, 1)));
        // Should be close to end of June 1 converted from Moscow to UTC
        Assert.That(result.TimeOfDay, Is.EqualTo(new TimeSpan(0, 20, 59, 59, 999).Add(TimeSpan.FromTicks(9999))));
    }

    // ──────────────────────────────────────────────────────
    // ToLocalDate
    // ──────────────────────────────────────────────────────

    [Test]
    public void ToLocalDate_ConvertsUtcToMoscowDate()
    {
        var svc = Build();
        // 2026-06-01 22:00:00 UTC = 2026-06-02 01:00:00 Moscow → date is June 2
        var utc = new DateTime(2026, 6, 1, 22, 0, 0, DateTimeKind.Utc);
        Assert.That(svc.ToLocalDate(utc), Is.EqualTo(new DateOnly(2026, 6, 2)));
    }

    [Test]
    public void ToLocalDate_UnspecifiedKind_TreatedAsUtc()
    {
        var svc = Build();
        // Unspecified kind – EnsureUtc should treat it as Utc
        var unspecified = DateTime.SpecifyKind(new DateTime(2026, 6, 1, 22, 0, 0), DateTimeKind.Unspecified);
        Assert.That(svc.ToLocalDate(unspecified), Is.EqualTo(new DateOnly(2026, 6, 2)));
    }

    [Test]
    public void ToLocalDate_LocalKind_ConvertsCorrectly()
    {
        var svc = Build();
        var localTime = DateTime.SpecifyKind(new DateTime(2026, 6, 1, 12, 0, 0), DateTimeKind.Local);
        // Result should be a valid DateOnly without throwing
        var result = svc.ToLocalDate(localTime);
        Assert.That(result.Year, Is.EqualTo(2026));
    }

    // ──────────────────────────────────────────────────────
    // IsActive
    // ──────────────────────────────────────────────────────

    [Test]
    public void IsActive_DateTime_ReturnsTrueWhenNowIsWithinRange()
    {
        var svc = Build(); // now = 2026-06-01 09:00 UTC
        var start = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2026, 6, 30, 23, 59, 59, DateTimeKind.Utc);
        Assert.That(svc.IsActive(start, end), Is.True);
    }

    [Test]
    public void IsActive_DateTime_ReturnsFalseWhenNowIsBeforeRange()
    {
        var svc = Build(); // now = 2026-06-01 09:00 UTC
        var start = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2026, 7, 31, 23, 59, 59, DateTimeKind.Utc);
        Assert.That(svc.IsActive(start, end), Is.False);
    }

    [Test]
    public void IsActive_DateTime_ReturnsFalseWhenNowIsAfterRange()
    {
        var svc = Build(); // now = 2026-06-01 09:00 UTC
        var start = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2026, 5, 31, 23, 59, 59, DateTimeKind.Utc);
        Assert.That(svc.IsActive(start, end), Is.False);
    }

    [Test]
    public void IsActive_DateTime_ReturnsTrueOnStartBoundary()
    {
        var now = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var svc = Build(now);
        Assert.That(svc.IsActive(now, now.AddDays(30)), Is.True);
    }

    [Test]
    public void IsActive_DateTime_ReturnsTrueOnEndBoundary()
    {
        var end = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var svc = Build(end);
        Assert.That(svc.IsActive(end.AddDays(-30), end), Is.True);
    }

    [Test]
    public void IsActive_Subscription_DelegatesToDateTimeOverload()
    {
        var svc = Build(); // now = 2026-06-01 09:00 UTC
        var subscription = new Subscription
        {
            Id = 1, AccountId = 1, CategoryId = 1,
            StartTime = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            EndTime = new DateTime(2026, 6, 30, 23, 59, 59, DateTimeKind.Utc)
        };
        Assert.That(svc.IsActive(subscription), Is.True);
    }

    [Test]
    public void IsActive_Subscription_ReturnsFalseForExpiredSubscription()
    {
        var svc = Build(); // now = 2026-06-01 09:00 UTC
        var subscription = new Subscription
        {
            Id = 1, AccountId = 1, CategoryId = 1,
            StartTime = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            EndTime = new DateTime(2026, 5, 31, 0, 0, 0, DateTimeKind.Utc)
        };
        Assert.That(svc.IsActive(subscription), Is.False);
    }

    // ──────────────────────────────────────────────────────
    // ResolveTimeZone – fallback to custom when ID is invalid
    // ──────────────────────────────────────────────────────

    [Test]
    public void ResolveTimeZone_InvalidId_FallsBackToMoscowOffset()
    {
        var svc = new SubscriptionTimeService(
            Options.Create(new SubscriptionSettings { TimeZoneId = "Invalid/TimeZone/That/Does/Not/Exist" }),
            new FixedClock(UtcReference));

        // Should still be UTC+3 because the fallback custom zone has +3h offset
        Assert.That(svc.TimeZone.BaseUtcOffset, Is.EqualTo(TimeSpan.FromHours(3)));
    }

    [Test]
    public void ResolveTimeZone_NullId_FallsBackToMoscow()
    {
        var svc = new SubscriptionTimeService(
            Options.Create(new SubscriptionSettings { TimeZoneId = null! }),
            new FixedClock(UtcReference));

        Assert.That(svc.TimeZone.BaseUtcOffset, Is.EqualTo(TimeSpan.FromHours(3)));
    }

    // ──────────────────────────────────────────────────────
    // SubscriptionClock – ensures production clock is UTC
    // ──────────────────────────────────────────────────────

    [Test]
    public void SubscriptionClock_UtcNow_IsUtcKind()
    {
        ISubscriptionClock clock = new SubscriptionClock();
        Assert.That(clock.UtcNow.Kind, Is.EqualTo(DateTimeKind.Utc));
    }

    private sealed class FixedClock(DateTime utcNow) : ISubscriptionClock
    {
        public DateTime UtcNow { get; } = DateTime.SpecifyKind(utcNow, DateTimeKind.Utc);
    }
}

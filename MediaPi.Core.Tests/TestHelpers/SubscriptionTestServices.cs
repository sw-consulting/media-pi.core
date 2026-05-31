// Copyright (C) 2025-2026 sw.consulting
// This file is a part of Media Pi backend

using MediaPi.Core.Data;
using MediaPi.Core.Services;
using MediaPi.Core.Services.Interfaces;
using MediaPi.Core.Settings;
using Microsoft.Extensions.Options;
using System;

namespace MediaPi.Core.Tests.TestHelpers;

internal static class SubscriptionTestServices
{
    public static ISubscriptionTimeService TimeService(DateTime? utcNow = null) =>
        new SubscriptionTimeService(
            Options.Create(new SubscriptionSettings { TimeZoneId = "Europe/Moscow" }),
            new FixedSubscriptionClock(utcNow ?? new DateTime(2026, 6, 1, 9, 0, 0, DateTimeKind.Utc)));

    public static IPlaylistAccessService PlaylistAccessService(AppDbContext db, DateTime? utcNow = null) =>
        new PlaylistAccessService(db, TimeService(utcNow));

    private sealed class FixedSubscriptionClock(DateTime utcNow) : ISubscriptionClock
    {
        public DateTime UtcNow { get; } = utcNow.Kind == DateTimeKind.Utc
            ? utcNow
            : DateTime.SpecifyKind(utcNow, DateTimeKind.Utc);
    }
}

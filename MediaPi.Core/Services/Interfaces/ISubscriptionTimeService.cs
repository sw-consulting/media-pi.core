// Copyright (C) 2025-2026 sw.consulting
// This file is a part of Media Pi backend

using MediaPi.Core.Models;

namespace MediaPi.Core.Services.Interfaces;

public interface ISubscriptionTimeService
{
    DateTime UtcNow { get; }
    DateTime LocalNow { get; }
    TimeZoneInfo TimeZone { get; }
    DateTime ToUtcStart(DateOnly localDate);
    DateTime ToUtcEnd(DateOnly localDate);
    DateOnly ToLocalDate(DateTime utcDateTime);
    bool IsActive(Subscription subscription);
    bool IsActive(DateTime startUtc, DateTime endUtc);
}

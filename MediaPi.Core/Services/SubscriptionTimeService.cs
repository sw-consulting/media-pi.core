// Copyright (C) 2025-2026 sw.consulting
// This file is a part of Media Pi backend

using MediaPi.Core.Models;
using MediaPi.Core.Services.Interfaces;
using MediaPi.Core.Settings;
using Microsoft.Extensions.Options;

namespace MediaPi.Core.Services;

public class SubscriptionTimeService : ISubscriptionTimeService
{
    private readonly ISubscriptionClock _clock;

    public SubscriptionTimeService(IOptions<SubscriptionSettings> options, ISubscriptionClock clock)
    {
        _clock = clock;
        TimeZone = ResolveTimeZone(options.Value.TimeZoneId);
    }

    public DateTime UtcNow => EnsureUtc(_clock.UtcNow);
    public DateTime LocalNow => TimeZoneInfo.ConvertTimeFromUtc(UtcNow, TimeZone);
    public TimeZoneInfo TimeZone { get; }

    public DateTime ToUtcStart(DateOnly localDate) =>
        TimeZoneInfo.ConvertTimeToUtc(localDate.ToDateTime(TimeOnly.MinValue), TimeZone);

    public DateTime ToUtcEnd(DateOnly localDate) =>
        TimeZoneInfo.ConvertTimeToUtc(localDate.ToDateTime(TimeOnly.MinValue).AddDays(1).AddTicks(-1), TimeZone);

    public DateOnly ToLocalDate(DateTime utcDateTime) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(EnsureUtc(utcDateTime), TimeZone));

    public bool IsActive(Subscription subscription) => IsActive(subscription.StartTime, subscription.EndTime);

    public bool IsActive(DateTime startUtc, DateTime endUtc)
    {
        var now = UtcNow;
        return EnsureUtc(startUtc) <= now && now <= EnsureUtc(endUtc);
    }

    private static DateTime EnsureUtc(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
    }

    private static TimeZoneInfo ResolveTimeZone(string? timeZoneId)
    {
        foreach (var id in CandidateTimeZoneIds(timeZoneId))
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(id);
            }
            catch (TimeZoneNotFoundException)
            {
            }
            catch (InvalidTimeZoneException)
            {
            }
        }

        return TimeZoneInfo.CreateCustomTimeZone("Europe/Moscow", TimeSpan.FromHours(3), "Europe/Moscow", "Europe/Moscow");
    }

    private static IEnumerable<string> CandidateTimeZoneIds(string? configured)
    {
        if (!string.IsNullOrWhiteSpace(configured)) yield return configured;
        yield return "Europe/Moscow";
        yield return "Russian Standard Time";
    }
}

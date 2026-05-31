// Copyright (C) 2025-2026 sw.consulting
// This file is a part of Media Pi backend

using System.Text.Json;
using MediaPi.Core.Models;
using MediaPi.Core.Services.Interfaces;
using MediaPi.Core.Settings;

namespace MediaPi.Core.RestModels;

public class SubscriptionViewItem
{
    public int Id { get; init; }
    public int AccountId { get; init; }
    public int CategoryId { get; init; }
    public string CategoryTitle { get; init; } = string.Empty;
    public DateOnly StartDate { get; init; }
    public DateOnly EndDate { get; init; }
    public bool IsActive { get; init; }

    public SubscriptionViewItem()
    {
    }

    public SubscriptionViewItem(Subscription subscription, ISubscriptionTimeService timeService)
    {
        Id = subscription.Id;
        AccountId = subscription.AccountId;
        CategoryId = subscription.CategoryId;
        CategoryTitle = subscription.Category?.Title ?? string.Empty;
        StartDate = timeService.ToLocalDate(subscription.StartTime);
        EndDate = timeService.ToLocalDate(subscription.EndTime);
        IsActive = timeService.IsActive(subscription);
    }

    public override string ToString() => JsonSerializer.Serialize(this, JOptions.DefaultOptions);
}

public class AccountSubscriptionsViewItem
{
    public List<SubscriptionViewItem> Subscriptions { get; init; } = [];
    public List<CategoryViewItem> AvailableCategories { get; init; } = [];

    public override string ToString() => JsonSerializer.Serialize(this, JOptions.DefaultOptions);
}

public class SubscriptionUpsertItem
{
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public bool ForcePlaylistCleanup { get; set; }

    public override string ToString() => JsonSerializer.Serialize(this, JOptions.DefaultOptions);
}

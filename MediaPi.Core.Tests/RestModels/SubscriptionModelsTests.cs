// Copyright (C) 2025-2026 sw.consulting
// This file is a part of Media Pi backend

using MediaPi.Core.Models;
using MediaPi.Core.RestModels;
using MediaPi.Core.Tests.TestHelpers;
using NUnit.Framework;
using System;
using System.Collections.Generic;

namespace MediaPi.Core.Tests.RestModels;

[TestFixture]
public class SubscriptionModelsTests
{
    private static readonly DateTime UtcReference = new(2026, 6, 1, 9, 0, 0, DateTimeKind.Utc);

    // ──────────────────────────────────────────────────────
    // SubscriptionViewItem – default constructor
    // ──────────────────────────────────────────────────────

    [Test]
    public void SubscriptionViewItem_DefaultConstructor_HasSensibleDefaults()
    {
        var item = new SubscriptionViewItem();
        Assert.That(item.Id, Is.EqualTo(0));
        Assert.That(item.CategoryTitle, Is.EqualTo(string.Empty));
        Assert.That(item.IsActive, Is.False);
    }

    // ──────────────────────────────────────────────────────
    // SubscriptionViewItem – subscription constructor
    // ──────────────────────────────────────────────────────

    [Test]
    public void SubscriptionViewItem_FromSubscription_ActiveWithinRange()
    {
        var category = new Category { Id = 3, Title = "Paid", Free = false };
        var subscription = new Subscription
        {
            Id = 7,
            AccountId = 2,
            CategoryId = category.Id,
            Category = category,
            StartTime = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            EndTime = new DateTime(2026, 6, 30, 23, 59, 59, DateTimeKind.Utc)
        };
        var timeService = SubscriptionTestServices.TimeService(UtcReference);

        var item = new SubscriptionViewItem(subscription, timeService);

        Assert.That(item.Id, Is.EqualTo(7));
        Assert.That(item.AccountId, Is.EqualTo(2));
        Assert.That(item.CategoryId, Is.EqualTo(3));
        Assert.That(item.CategoryTitle, Is.EqualTo("Paid"));
        Assert.That(item.IsActive, Is.True);
        // StartDate should be June 1 in Moscow time (UTC+3): start of UTC June 1 → Moscow June 1
        Assert.That(item.StartDate, Is.EqualTo(new DateOnly(2026, 6, 1)));
        // End UTC 2026-06-30 23:59:59 → Moscow 2026-07-01 02:59:59 → date is July 1
        Assert.That(item.EndDate, Is.EqualTo(new DateOnly(2026, 7, 1)));
    }

    [Test]
    public void SubscriptionViewItem_FromSubscription_InactiveWhenExpired()
    {
        var category = new Category { Id = 3, Title = "Paid", Free = false };
        var subscription = new Subscription
        {
            Id = 8,
            AccountId = 2,
            CategoryId = category.Id,
            Category = category,
            StartTime = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            EndTime = new DateTime(2026, 5, 31, 23, 59, 59, DateTimeKind.Utc)
        };
        var timeService = SubscriptionTestServices.TimeService(UtcReference);

        var item = new SubscriptionViewItem(subscription, timeService);

        Assert.That(item.IsActive, Is.False);
    }

    [Test]
    public void SubscriptionViewItem_FromSubscription_NullCategory_UsesEmptyTitle()
    {
        var subscription = new Subscription
        {
            Id = 9,
            AccountId = 2,
            CategoryId = 3,
            Category = null!,
            StartTime = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            EndTime = new DateTime(2026, 6, 30, 23, 59, 59, DateTimeKind.Utc)
        };
        var timeService = SubscriptionTestServices.TimeService(UtcReference);

        var item = new SubscriptionViewItem(subscription, timeService);

        Assert.That(item.CategoryTitle, Is.EqualTo(string.Empty));
    }

    // ──────────────────────────────────────────────────────
    // ToString – produces valid JSON
    // ──────────────────────────────────────────────────────

    [Test]
    public void SubscriptionViewItem_ToString_ContainsId()
    {
        var item = new SubscriptionViewItem { Id = 42 };
        Assert.That(item.ToString(), Does.Contain("42"));
    }

    [Test]
    public void AccountSubscriptionsViewItem_ToString_ContainsSubscriptions()
    {
        var viewItem = new AccountSubscriptionsViewItem
        {
            Subscriptions = [new SubscriptionViewItem { Id = 10 }],
            AvailableCategories = []
        };
        Assert.That(viewItem.ToString(), Does.Contain("10"));
    }

    [Test]
    public void SubscriptionUpsertItem_ToString_ContainsDates()
    {
        var item = new SubscriptionUpsertItem
        {
            StartDate = new DateOnly(2026, 6, 1),
            EndDate = new DateOnly(2026, 6, 30)
        };
        Assert.That(item.ToString(), Does.Contain("2026"));
    }

    // ──────────────────────────────────────────────────────
    // PlaylistAccessImpact
    // ──────────────────────────────────────────────────────

    [Test]
    public void PlaylistAccessImpact_HasImpact_TrueWhenAffectedItemCountPositive()
    {
        var impact = new PlaylistAccessImpact { AffectedItemCount = 3 };
        Assert.That(impact.HasImpact, Is.True);
    }

    [Test]
    public void PlaylistAccessImpact_HasImpact_FalseWhenZero()
    {
        var impact = new PlaylistAccessImpact();
        Assert.That(impact.HasImpact, Is.False);
    }

    [Test]
    public void PlaylistAccessImpact_ToString_ContainsCounts()
    {
        var impact = new PlaylistAccessImpact
        {
            AffectedPlaylistCount = 2,
            AffectedItemCount = 5,
            AffectedVideoCount = 3,
            AffectedPlaylists = []
        };
        var json = impact.ToString();
        Assert.That(json, Does.Contain("2"));
        Assert.That(json, Does.Contain("5"));
    }

    [Test]
    public void PlaylistCleanupResult_DefaultsToZero()
    {
        var result = new PlaylistCleanupResult();
        Assert.That(result.RemovedItemCount, Is.EqualTo(0));
        Assert.That(result.AffectedPlaylistCount, Is.EqualTo(0));
        Assert.That(result.AffectedVideoCount, Is.EqualTo(0));
    }
}

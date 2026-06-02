// Copyright (C) 2025-2026 sw.consulting
// This file is a part of Media Pi backend

using System;
using System.Linq;
using System.Threading.Tasks;
using MediaPi.Core.Data;
using MediaPi.Core.Models;
using MediaPi.Core.Services.Interfaces;
using MediaPi.Core.Tests.TestHelpers;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;

namespace MediaPi.Core.Tests.Services;

[TestFixture]
public class PlaylistAccessServiceTests
{
#pragma warning disable CS8618
    private AppDbContext _db;
    private IPlaylistAccessService _service;
    private Account _account;
    private Category _freeCategory;
    private Category _paidCategory;
#pragma warning restore CS8618

    [SetUp]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"playlist_access_service_{Guid.NewGuid()}")
            .Options;
        _db = new AppDbContext(options);
        _service = SubscriptionTestServices.PlaylistAccessService(_db);

        _account = new Account { Id = 1, Name = "Account" };
        _freeCategory = new Category { Id = 1, Title = "Free", Free = true };
        _paidCategory = new Category { Id = 2, Title = "Paid", Free = false };
        _db.Accounts.Add(_account);
        _db.Categories.AddRange(_freeCategory, _paidCategory);
        _db.SaveChanges();
    }

    [TearDown]
    public void TearDown()
    {
        _db.Database.EnsureDeleted();
        _db.Dispose();
    }

    [Test]
    public async Task GetAccessibleVideoIdsForAccount_AppliesCommonCategoryAndSubscriptionRules()
    {
        var uncategorized = AddVideo(1, null, null);
        var free = AddVideo(2, null, _freeCategory.Id);
        var paid = AddVideo(3, null, _paidCategory.Id);
        var accountOwned = AddVideo(4, _account.Id, null);
        var otherOwned = AddVideo(5, 99, null);
        _db.Subscriptions.Add(new Subscription
        {
            AccountId = _account.Id,
            CategoryId = _paidCategory.Id,
            StartTime = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc),
            EndTime = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc)
        });
        await _db.SaveChangesAsync();

        var accessible = await _service.GetAccessibleVideoIdsForAccountAsync(
            _account.Id,
            new[] { uncategorized.Id, free.Id, paid.Id, accountOwned.Id, otherOwned.Id });

        Assert.That(accessible, Is.EquivalentTo(new[] { uncategorized.Id, free.Id, paid.Id, accountOwned.Id }));
    }

    [Test]
    public async Task BuildCategoryFreeChangeImpact_ReturnsAffectedPlaylistList()
    {
        var video = AddVideo(10, null, _freeCategory.Id);
        var playlist = new Playlist { Id = 10, Title = "Main", Filename = "main.m3u", AccountId = _account.Id, Account = _account };
        _db.Playlists.Add(playlist);
        _db.VideoPlaylists.AddRange(
            new VideoPlaylist { Id = 100, PlaylistId = playlist.Id, Playlist = playlist, VideoId = video.Id, Video = video, Position = 0 },
            new VideoPlaylist { Id = 101, PlaylistId = playlist.Id, Playlist = playlist, VideoId = video.Id, Video = video, Position = 1 });
        await _db.SaveChangesAsync();

        var impact = await _service.BuildCategoryFreeChangeImpactAsync(_freeCategory.Id, false);

        Assert.That(impact.AffectedPlaylistCount, Is.EqualTo(1));
        Assert.That(impact.AffectedItemCount, Is.EqualTo(2));
        Assert.That(impact.AffectedVideoCount, Is.EqualTo(1));
        Assert.That(impact.AffectedPlaylists.Single().Title, Is.EqualTo("Main"));
        Assert.That(impact.AffectedPlaylists.Single().RemovedItemCount, Is.EqualTo(2));
        Assert.That(impact.VideoPlaylistIds, Is.EquivalentTo(new[] { 100, 101 }));
    }

    [Test]
    public async Task RemoveCurrentInvalidPlaylistItems_RemovesExpiredSubscriptionRows()
    {
        var video = AddVideo(20, null, _paidCategory.Id);
        var playlist = new Playlist { Id = 20, Title = "Expired", Filename = "expired.m3u", AccountId = _account.Id, Account = _account };
        _db.Playlists.Add(playlist);
        _db.VideoPlaylists.Add(new VideoPlaylist { Id = 200, PlaylistId = playlist.Id, Playlist = playlist, VideoId = video.Id, Video = video, Position = 0 });
        _db.Subscriptions.Add(new Subscription
        {
            AccountId = _account.Id,
            CategoryId = _paidCategory.Id,
            StartTime = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            EndTime = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc)
        });
        await _db.SaveChangesAsync();

        var result = await _service.RemoveCurrentInvalidPlaylistItemsAsync();

        Assert.That(result.RemovedItemCount, Is.EqualTo(1));
        Assert.That(await _db.VideoPlaylists.AnyAsync(vp => vp.Id == 200), Is.False);
    }

    // ──────────────────────────────────────────────────────
    // GetInaccessibleVideoIds
    // ──────────────────────────────────────────────────────

    [Test]
    public async Task GetInaccessibleVideoIds_EmptyList_ReturnsEmpty()
    {
        var result = await _service.GetInaccessibleVideoIdsForAccountAsync(_account.Id, []);
        Assert.That(result, Is.Empty);
    }

    [Test]
    public async Task GetInaccessibleVideoIds_PaidVideoWithoutSubscription_IsInaccessible()
    {
        var paid = AddVideo(30, null, _paidCategory.Id);
        await _db.SaveChangesAsync();

        var result = await _service.GetInaccessibleVideoIdsForAccountAsync(_account.Id, [paid.Id]);

        Assert.That(result, Is.EquivalentTo(new[] { paid.Id }));
    }

    [Test]
    public async Task GetInaccessibleVideoIds_FreeVideo_IsAccessible()
    {
        var free = AddVideo(31, null, _freeCategory.Id);
        await _db.SaveChangesAsync();

        var result = await _service.GetInaccessibleVideoIdsForAccountAsync(_account.Id, [free.Id]);

        Assert.That(result, Is.Empty);
    }

    // ──────────────────────────────────────────────────────
    // AccountCanAccessVideo
    // ──────────────────────────────────────────────────────

    [Test]
    public async Task AccountCanAccessVideo_OwnVideo_ReturnsTrue()
    {
        var own = AddVideo(40, _account.Id, null);
        await _db.SaveChangesAsync();

        var result = await _service.AccountCanAccessVideoAsync(_account.Id, own.Id);

        Assert.That(result, Is.True);
    }

    [Test]
    public async Task AccountCanAccessVideo_OtherAccountVideo_ReturnsFalse()
    {
        var other = AddVideo(41, 99, null);
        await _db.SaveChangesAsync();

        var result = await _service.AccountCanAccessVideoAsync(_account.Id, other.Id);

        Assert.That(result, Is.False);
    }

    [Test]
    public async Task AccountCanAccessVideo_PaidWithActiveSubscription_ReturnsTrue()
    {
        var paid = AddVideo(42, null, _paidCategory.Id);
        _db.Subscriptions.Add(new Subscription
        {
            AccountId = _account.Id,
            CategoryId = _paidCategory.Id,
            StartTime = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc),
            EndTime = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc)
        });
        await _db.SaveChangesAsync();

        var result = await _service.AccountCanAccessVideoAsync(_account.Id, paid.Id);

        Assert.That(result, Is.True);
    }

    // ──────────────────────────────────────────────────────
    // GetAccessibleVideoIds – empty input
    // ──────────────────────────────────────────────────────

    [Test]
    public async Task GetAccessibleVideoIds_EmptyList_ReturnsEmpty()
    {
        var result = await _service.GetAccessibleVideoIdsForAccountAsync(_account.Id, []);
        Assert.That(result, Is.Empty);
    }

    [Test]
    public async Task GetAccessibleVideoIds_UncategorizedCommonVideo_IsAccessible()
    {
        var uncategorized = AddVideo(50, null, null);
        await _db.SaveChangesAsync();

        var result = await _service.GetAccessibleVideoIdsForAccountAsync(_account.Id, [uncategorized.Id]);

        Assert.That(result, Does.Contain(uncategorized.Id));
    }

    // ──────────────────────────────────────────────────────
    // BuildCurrentInvalidPlaylistImpactAsync
    // ──────────────────────────────────────────────────────

    [Test]
    public async Task BuildCurrentInvalidPlaylistImpact_NoPlaylists_ReturnsEmptyImpact()
    {
        var impact = await _service.BuildCurrentInvalidPlaylistImpactAsync();
        Assert.That(impact.HasImpact, Is.False);
    }

    // ──────────────────────────────────────────────────────
    // BuildSubscriptionChangeImpactAsync
    // ──────────────────────────────────────────────────────

    [Test]
    public async Task BuildSubscriptionChangeImpact_SubscriptionBecomesActive_NoImpact()
    {
        // Video in paid category, playlist belongs to account
        var video = AddVideo(60, null, _paidCategory.Id);
        var playlist = new Playlist { Id = 60, Title = "P60", Filename = "p60.m3u", AccountId = _account.Id, Account = _account };
        _db.Playlists.Add(playlist);
        _db.VideoPlaylists.Add(new VideoPlaylist { Id = 600, PlaylistId = playlist.Id, Playlist = playlist, VideoId = video.Id, Video = video, Position = 0 });
        await _db.SaveChangesAsync();

        // Propose a start/end that covers "now" (UtcNow = 2026-06-01 09:00)
        var start = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2026, 6, 30, 23, 59, 59, DateTimeKind.Utc);
        var impact = await _service.BuildSubscriptionChangeImpactAsync(_account.Id, _paidCategory.Id, start, end);

        // Active subscription → video accessible → no impact
        Assert.That(impact.HasImpact, Is.False);
    }

    [Test]
    public async Task BuildSubscriptionChangeImpact_SubscriptionBecomesInactive_ReturnsImpact()
    {
        // Video in paid category, playlist belongs to account
        var video = AddVideo(61, null, _paidCategory.Id);
        var playlist = new Playlist { Id = 61, Title = "P61", Filename = "p61.m3u", AccountId = _account.Id, Account = _account };
        _db.Playlists.Add(playlist);
        _db.VideoPlaylists.Add(new VideoPlaylist { Id = 610, PlaylistId = playlist.Id, Playlist = playlist, VideoId = video.Id, Video = video, Position = 0 });
        await _db.SaveChangesAsync();

        // Propose a start/end that is in the future (UtcNow = 2026-06-01 09:00)
        var start = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2026, 7, 31, 23, 59, 59, DateTimeKind.Utc);
        var impact = await _service.BuildSubscriptionChangeImpactAsync(_account.Id, _paidCategory.Id, start, end);

        // Inactive subscription → video not accessible → has impact
        Assert.That(impact.HasImpact, Is.True);
        Assert.That(impact.VideoPlaylistIds, Does.Contain(610));
    }

    // ──────────────────────────────────────────────────────
    // BuildVideoCategoryChangeImpactAsync
    // ──────────────────────────────────────────────────────

    [Test]
    public async Task BuildVideoCategoryChangeImpact_EmptyVideoIds_ReturnsNoImpact()
    {
        var impact = await _service.BuildVideoCategoryChangeImpactAsync([], null);
        Assert.That(impact.HasImpact, Is.False);
    }

    [Test]
    public async Task BuildVideoCategoryChangeImpact_MovingToPaidCategory_ReturnsImpact()
    {
        // Free video in a playlist; propose to move it to paid category (no active subscription)
        var video = AddVideo(70, null, _freeCategory.Id);
        var playlist = new Playlist { Id = 70, Title = "P70", Filename = "p70.m3u", AccountId = _account.Id, Account = _account };
        _db.Playlists.Add(playlist);
        _db.VideoPlaylists.Add(new VideoPlaylist { Id = 700, PlaylistId = playlist.Id, Playlist = playlist, VideoId = video.Id, Video = video, Position = 0 });
        await _db.SaveChangesAsync();

        var impact = await _service.BuildVideoCategoryChangeImpactAsync([video.Id], _paidCategory.Id);

        Assert.That(impact.HasImpact, Is.True);
        Assert.That(impact.VideoPlaylistIds, Does.Contain(700));
    }

    // ──────────────────────────────────────────────────────
    // RemovePlaylistItemsAsync – edge cases
    // ──────────────────────────────────────────────────────

    [Test]
    public async Task RemovePlaylistItems_EmptyList_ReturnsZeroCounts()
    {
        var result = await _service.RemovePlaylistItemsAsync([]);
        Assert.That(result.RemovedItemCount, Is.EqualTo(0));
    }

    [Test]
    public async Task RemovePlaylistItems_NonExistentIds_ReturnsZeroCounts()
    {
        var result = await _service.RemovePlaylistItemsAsync([99999, 88888]);
        Assert.That(result.RemovedItemCount, Is.EqualTo(0));
    }

    [Test]
    public async Task RemovePlaylistItems_ValidIds_RemovesAndReturnsCorrectCounts()
    {
        var video = AddVideo(80, null, _freeCategory.Id);
        var playlist = new Playlist { Id = 80, Title = "P80", Filename = "p80.m3u", AccountId = _account.Id, Account = _account };
        _db.Playlists.Add(playlist);
        _db.VideoPlaylists.AddRange(
            new VideoPlaylist { Id = 800, PlaylistId = playlist.Id, Playlist = playlist, VideoId = video.Id, Video = video, Position = 0 },
            new VideoPlaylist { Id = 801, PlaylistId = playlist.Id, Playlist = playlist, VideoId = video.Id, Video = video, Position = 1 });
        await _db.SaveChangesAsync();

        var result = await _service.RemovePlaylistItemsAsync([800, 801]);

        Assert.That(result.RemovedItemCount, Is.EqualTo(2));
        Assert.That(result.AffectedPlaylistCount, Is.EqualTo(1));
        Assert.That(result.AffectedVideoCount, Is.EqualTo(1));
        Assert.That(await _db.VideoPlaylists.AnyAsync(vp => vp.Id == 800 || vp.Id == 801), Is.False);
    }

    // ──────────────────────────────────────────────────────
    // BuildCategoryFreeChangeImpact – becomes free (no impact)
    // ──────────────────────────────────────────────────────

    [Test]
    public async Task BuildCategoryFreeChangeImpact_BecomingFree_NoImpact()
    {
        // Paid video in playlist; propose to make category free
        var video = AddVideo(90, null, _paidCategory.Id);
        var playlist = new Playlist { Id = 90, Title = "P90", Filename = "p90.m3u", AccountId = _account.Id, Account = _account };
        _db.Playlists.Add(playlist);
        _db.VideoPlaylists.Add(new VideoPlaylist { Id = 900, PlaylistId = playlist.Id, Playlist = playlist, VideoId = video.Id, Video = video, Position = 0 });
        await _db.SaveChangesAsync();

        var impact = await _service.BuildCategoryFreeChangeImpactAsync(_paidCategory.Id, proposedFree: true);

        // Making category free means all users can access → no removal needed
        Assert.That(impact.HasImpact, Is.False);
    }

    // ──────────────────────────────────────────────────────
    // RemoveCurrentInvalidPlaylistItemsAsync
    // ──────────────────────────────────────────────────────

    [Test]
    public async Task RemoveCurrentInvalidPlaylistItems_AllAccessible_ReturnsZero()
    {
        // All videos are owned by the same account as the playlists → always accessible
        var video = AddVideo(95, _account.Id, null);
        var playlist = new Playlist { Id = 95, Title = "P95", Filename = "p95.m3u", AccountId = _account.Id, Account = _account };
        _db.Playlists.Add(playlist);
        _db.VideoPlaylists.Add(new VideoPlaylist { Id = 950, PlaylistId = playlist.Id, Playlist = playlist, VideoId = video.Id, Video = video, Position = 0 });
        await _db.SaveChangesAsync();

        var result = await _service.RemoveCurrentInvalidPlaylistItemsAsync();

        Assert.That(result.RemovedItemCount, Is.EqualTo(0));
        Assert.That(await _db.VideoPlaylists.AnyAsync(vp => vp.Id == 950), Is.True);
    }

    private Video AddVideo(int id, int? accountId, int? categoryId)
    {
        var video = new Video
        {
            Id = id,
            Title = $"Video {id}",
            Filename = $"video-{id}.mp4",
            OriginalFilename = $"video-{id}.mp4",
            FileSizeBytes = 100,
            AccountId = accountId,
            CategoryId = categoryId,
            Sha256 = new string('a', 64)
        };
        _db.Videos.Add(video);
        return video;
    }
}

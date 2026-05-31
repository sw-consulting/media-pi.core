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

// Copyright (C) 2025-2026 sw.consulting
// This file is a part of Media Pi backend

using System;
using System.Threading.Tasks;
using MediaPi.Core.Data;
using MediaPi.Core.Models;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;

namespace MediaPi.Core.Tests.Data;

[TestFixture]
public class AppDbContextPlaylistTimestampTests
{
    [Test]
    public async Task SaveChangesAsync_NewPlaylist_SetsCreatedAtAndUpdatedAt()
    {
        await using var db = CreateDbContext();
        db.Playlists.Add(new Playlist
        {
            Title = "New",
            Filename = "new.m3u",
            AccountId = 1
        });
        var startedAt = DateTime.UtcNow.AddSeconds(-1);

        await db.SaveChangesAsync();
        var finishedAt = DateTime.UtcNow.AddSeconds(1);

        var playlist = await db.Playlists.SingleAsync();
        Assert.That(playlist.CreatedAt, Is.InRange(startedAt, finishedAt));
        Assert.That(playlist.UpdatedAt, Is.EqualTo(playlist.CreatedAt));
    }

    [Test]
    public async Task SaveChangesAsync_PlaylistContentChanged_RefreshesUpdatedAt()
    {
        await using var db = CreateDbContext();
        var createdAt = new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        var oldUpdatedAt = new DateTime(2026, 1, 2, 10, 0, 0, DateTimeKind.Utc);
        db.Playlists.Add(new Playlist
        {
            Id = 10,
            Title = "Old",
            Filename = "old.m3u",
            AccountId = 1,
            CreatedAt = createdAt,
            UpdatedAt = oldUpdatedAt
        });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var playlist = await db.Playlists.SingleAsync(p => p.Id == 10);
        playlist.Title = "New";
        var startedAt = DateTime.UtcNow.AddSeconds(-1);

        await db.SaveChangesAsync();
        var finishedAt = DateTime.UtcNow.AddSeconds(1);

        Assert.That(playlist.CreatedAt, Is.EqualTo(createdAt));
        Assert.That(playlist.UpdatedAt, Is.InRange(startedAt, finishedAt));
        Assert.That(playlist.UpdatedAt, Is.GreaterThan(oldUpdatedAt));
    }

    [Test]
    public async Task SaveChangesAsync_VideoPlaylistAdded_RefreshesPlaylistUpdatedAt()
    {
        await using var db = CreateDbContext();
        var oldUpdatedAt = new DateTime(2026, 1, 2, 10, 0, 0, DateTimeKind.Utc);
        await SeedPlaylistAndVideo(db, playlistId: 20, videoId: 20, oldUpdatedAt);
        db.ChangeTracker.Clear();

        db.VideoPlaylists.Add(new VideoPlaylist { Id = 20, PlaylistId = 20, VideoId = 20, Position = 0 });
        var startedAt = DateTime.UtcNow.AddSeconds(-1);

        await db.SaveChangesAsync();
        var finishedAt = DateTime.UtcNow.AddSeconds(1);

        var playlist = await db.Playlists.SingleAsync(p => p.Id == 20);
        Assert.That(playlist.Title, Is.EqualTo("Playlist 20"));
        Assert.That(playlist.UpdatedAt, Is.InRange(startedAt, finishedAt));
        Assert.That(playlist.UpdatedAt, Is.GreaterThan(oldUpdatedAt));
    }

    [Test]
    public async Task SaveChangesAsync_VideoPlaylistDeleted_RefreshesPlaylistUpdatedAt()
    {
        await using var db = CreateDbContext();
        var oldUpdatedAt = new DateTime(2026, 1, 2, 10, 0, 0, DateTimeKind.Utc);
        await SeedPlaylistAndVideo(db, playlistId: 30, videoId: 30, oldUpdatedAt);
        db.VideoPlaylists.Add(new VideoPlaylist { Id = 30, PlaylistId = 30, VideoId = 30, Position = 0 });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var item = await db.VideoPlaylists.SingleAsync(vp => vp.Id == 30);
        db.VideoPlaylists.Remove(item);
        var startedAt = DateTime.UtcNow.AddSeconds(-1);

        await db.SaveChangesAsync();
        var finishedAt = DateTime.UtcNow.AddSeconds(1);

        var playlist = await db.Playlists.SingleAsync(p => p.Id == 30);
        Assert.That(playlist.Title, Is.EqualTo("Playlist 30"));
        Assert.That(playlist.UpdatedAt, Is.InRange(startedAt, finishedAt));
        Assert.That(playlist.UpdatedAt, Is.GreaterThan(oldUpdatedAt));
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"playlist_timestamp_test_db_{Guid.NewGuid()}")
            .Options;

        return new AppDbContext(options);
    }

    private static async Task SeedPlaylistAndVideo(AppDbContext db, int playlistId, int videoId, DateTime updatedAt)
    {
        db.Playlists.Add(new Playlist
        {
            Id = playlistId,
            Title = $"Playlist {playlistId}",
            Filename = $"playlist-{playlistId}.m3u",
            AccountId = 1,
            CreatedAt = new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc),
            UpdatedAt = updatedAt
        });
        db.Videos.Add(new Video
        {
            Id = videoId,
            Title = $"Video {videoId}",
            Filename = $"video-{videoId}.mp4",
            OriginalFilename = $"video-{videoId}.mp4",
            FileSizeBytes = 100
        });
        await db.SaveChangesAsync();
    }
}

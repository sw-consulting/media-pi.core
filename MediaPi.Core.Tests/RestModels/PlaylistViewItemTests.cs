// Copyright (C) 2025-2026 sw.consulting
// This file is a part of Media Pi backend

using MediaPi.Core.Models;
using MediaPi.Core.RestModels;
using NUnit.Framework;
using System;
using System.Text.Json;

namespace MediaPi.Core.Tests.RestModels;

[TestFixture]
public class PlaylistViewItemTests
{
    [Test]
    public void Constructor_MapsPlaylistTimestamps()
    {
        var createdAt = new DateTime(2026, 6, 24, 8, 30, 0, DateTimeKind.Utc);
        var updatedAt = new DateTime(2026, 6, 24, 9, 45, 0, DateTimeKind.Utc);
        var playlist = new Playlist
        {
            Id = 42,
            Title = "Morning",
            Filename = "morning.m3u",
            AccountId = 7,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt,
            VideosPlaylist = []
        };

        var item = new PlaylistViewItem(playlist);

        Assert.That(item.CreatedAt, Is.EqualTo(createdAt));
        Assert.That(item.UpdatedAt, Is.EqualTo(updatedAt));
    }

    [Test]
    public void ToString_IncludesPlaylistTimestamps()
    {
        var playlist = new Playlist
        {
            Id = 42,
            Title = "Morning",
            Filename = "morning.m3u",
            AccountId = 7,
            CreatedAt = new DateTime(2026, 6, 24, 8, 30, 0, DateTimeKind.Utc),
            UpdatedAt = new DateTime(2026, 6, 24, 9, 45, 0, DateTimeKind.Utc),
            VideosPlaylist = []
        };

        var item = new PlaylistViewItem(playlist);
        var json = item.ToString();

        using var doc = JsonDocument.Parse(json);
        Assert.That(doc.RootElement.GetProperty(nameof(PlaylistViewItem.CreatedAt)).GetDateTime(), Is.EqualTo(item.CreatedAt));
        Assert.That(doc.RootElement.GetProperty(nameof(PlaylistViewItem.UpdatedAt)).GetDateTime(), Is.EqualTo(item.UpdatedAt));
    }
}

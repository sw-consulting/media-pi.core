// Copyright (C) 2025-2026 sw.consulting
// This file is a part of Media Pi backend

using System;
using System.Linq;
using MediaPi.Core.Data;
using MediaPi.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using NUnit.Framework;

namespace MediaPi.Core.Tests.Data;

[TestFixture]
public class AppDbContextCategoryTests
{
    [Test]
    public void CategoryFree_HasDatabaseDefaultButIsSavedExplicitlyByEf()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"category_model_test_db_{Guid.NewGuid()}")
            .Options;

        using var db = new AppDbContext(options);

        var property = db.Model
            .FindEntityType(typeof(Category))!
            .FindProperty(nameof(Category.Free))!;

        Assert.That(property.GetDefaultValue(), Is.EqualTo(true));
        Assert.That(property.ValueGenerated, Is.EqualTo(ValueGenerated.Never));
    }
    [Test]
    public void VideoOriginalFilename_HasUniqueContainerIndexes()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"video_original_filename_model_test_db_{Guid.NewGuid()}")
            .Options;

        using var db = new AppDbContext(options);

        var indexes = db.Model
            .FindEntityType(typeof(Video))!
            .GetIndexes()
            .ToDictionary(index => index.GetDatabaseName()!);

        AssertVideoIndex(
            indexes["IX_videos_account_id_original_filename"],
            [nameof(Video.AccountId), nameof(Video.OriginalFilename)],
            "\"account_id\" IS NOT NULL");
        AssertVideoIndex(
            indexes["IX_videos_category_id_original_filename"],
            [nameof(Video.CategoryId), nameof(Video.OriginalFilename)],
            "\"account_id\" IS NULL AND \"category_id\" IS NOT NULL");
        AssertVideoIndex(
            indexes["IX_videos_common_uncategorized_original_filename"],
            [nameof(Video.OriginalFilename)],
            "\"account_id\" IS NULL AND \"category_id\" IS NULL");
    }

    [Test]
    public void VideoDescription_HasUniqueContainerIndexes()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"video_description_model_test_db_{Guid.NewGuid()}")
            .Options;

        using var db = new AppDbContext(options);

        var indexes = db.Model
            .FindEntityType(typeof(Video))!
            .GetIndexes()
            .ToDictionary(index => index.GetDatabaseName()!);

        AssertVideoIndex(
            indexes["IX_videos_account_id_title"],
            [nameof(Video.AccountId), nameof(Video.Title)],
            "\"account_id\" IS NOT NULL");
        AssertVideoIndex(
            indexes["IX_videos_category_id_title"],
            [nameof(Video.CategoryId), nameof(Video.Title)],
            "\"account_id\" IS NULL AND \"category_id\" IS NOT NULL");
        AssertVideoIndex(
            indexes["IX_videos_common_uncategorized_title"],
            [nameof(Video.Title)],
            "\"account_id\" IS NULL AND \"category_id\" IS NULL");
    }

    [Test]
    public void PlaylistTitle_HasUniqueAccountIndex()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"playlist_title_model_test_db_{Guid.NewGuid()}")
            .Options;

        using var db = new AppDbContext(options);

        var index = db.Model
            .FindEntityType(typeof(Playlist))!
            .GetIndexes()
            .Single(index => index.GetDatabaseName() == "IX_playlists_account_id_title");

        Assert.That(index.IsUnique, Is.True);
        Assert.That(index.Properties.Select(property => property.Name), Is.EqualTo(
            [nameof(Playlist.AccountId), nameof(Playlist.Title)]));
    }

    private static void AssertVideoIndex(IIndex index, string[] propertyNames, string filter)
    {
        Assert.That(index.IsUnique, Is.True);
        Assert.That(index.Properties.Select(property => property.Name), Is.EqualTo(propertyNames));
        Assert.That(index.GetFilter(), Is.EqualTo(filter));
    }
}

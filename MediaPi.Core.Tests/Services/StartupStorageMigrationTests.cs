// Copyright (C) 2026 sw.consulting
// This file is a part of Media Pi backend

using System;
using System.IO;
using System.Threading.Tasks;
using MediaPi.Core.Data;
using MediaPi.Core.Models;
using MediaPi.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace MediaPi.Core.Tests.Services;

[TestFixture]
public class StartupStorageMigrationTests
{
    private string _legacyRoot = null!;
    private string _videoRoot = null!;
    private string _screenshotRoot = null!;
    private DbContextOptions<AppDbContext> _dbOptions = null!;

    [SetUp]
    public void SetUp()
    {
        _legacyRoot = Path.Combine(Path.GetTempPath(), $"storage_migration_legacy_{Guid.NewGuid():N}");
        _videoRoot = Path.Combine(_legacyRoot, "video");
        _screenshotRoot = Path.Combine(_legacyRoot, "screenshots");

        Directory.CreateDirectory(_legacyRoot);

        _dbOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"StartupStorageMigrationTests_{Guid.NewGuid():N}")
            .Options;
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_legacyRoot))
        {
            Directory.Delete(_legacyRoot, true);
        }
    }

    [Test]
    public async Task RunAsync_MovesVideoAndScreenshotFilesToNewRoots()
    {
        var videoRelative = "0001/video-a.mp4";
        var screenshotRelative = "0002/screen-a.jpg";

        var oldVideoPath = Path.Combine(_legacyRoot, videoRelative.Replace('/', Path.DirectorySeparatorChar));
        var oldScreenshotPath = Path.Combine(_legacyRoot, screenshotRelative.Replace('/', Path.DirectorySeparatorChar));

        Directory.CreateDirectory(Path.GetDirectoryName(oldVideoPath)!);
        Directory.CreateDirectory(Path.GetDirectoryName(oldScreenshotPath)!);

        await File.WriteAllTextAsync(oldVideoPath, "video");
        await File.WriteAllTextAsync(oldScreenshotPath, "screenshot");

        await using (var db = new AppDbContext(_dbOptions))
        {
            db.Videos.Add(new Video
            {
                Id = 1,
                Title = "v1",
                Filename = videoRelative,
                OriginalFilename = "video-a.mp4",
                FileSizeBytes = 5,
                DurationSeconds = 1
            });

            db.Screenshots.Add(new Screenshot
            {
                Id = 1,
                Filename = screenshotRelative,
                OriginalFilename = "screen-a.jpg",
                FileSizeBytes = 10,
                TimeCreated = DateTime.UtcNow,
                DeviceId = 1
            });

            await db.SaveChangesAsync();
        }

        await using (var db = new AppDbContext(_dbOptions))
        {
            await StartupStorageMigration.RunAsync(db, _videoRoot, _screenshotRoot, NullLogger.Instance, _legacyRoot, default);
        }

        var newVideoPath = Path.Combine(_videoRoot, videoRelative.Replace('/', Path.DirectorySeparatorChar));
        var newScreenshotPath = Path.Combine(_screenshotRoot, screenshotRelative.Replace('/', Path.DirectorySeparatorChar));

        Assert.Multiple(() =>
        {
            Assert.That(File.Exists(newVideoPath), Is.True);
            Assert.That(File.Exists(newScreenshotPath), Is.True);
            Assert.That(File.Exists(oldVideoPath), Is.False);
            Assert.That(File.Exists(oldScreenshotPath), Is.False);
        });
    }

    [Test]
    public async Task RunAsync_WhenDestinationAlreadyExists_DeletesLegacyDuplicate()
    {
        var markerDirectory = Path.Combine(_legacyRoot, ".migration");
        Directory.CreateDirectory(markerDirectory);
        var markerPath = Path.Combine(markerDirectory, $"storage-layout-{VersionInfo.AppVersion}.done");
        await File.WriteAllTextAsync(markerPath, "done");

        var videoRelative = "0003/video-dup.mp4";

        var oldVideoPath = Path.Combine(_legacyRoot, videoRelative.Replace('/', Path.DirectorySeparatorChar));
        var newVideoPath = Path.Combine(_videoRoot, videoRelative.Replace('/', Path.DirectorySeparatorChar));

        Directory.CreateDirectory(Path.GetDirectoryName(oldVideoPath)!);
        Directory.CreateDirectory(Path.GetDirectoryName(newVideoPath)!);

        await File.WriteAllTextAsync(oldVideoPath, "legacy-video");
        await File.WriteAllTextAsync(newVideoPath, "new-video");

        await using (var db = new AppDbContext(_dbOptions))
        {
            db.Videos.Add(new Video
            {
                Id = 11,
                Title = "v11",
                Filename = videoRelative,
                OriginalFilename = "video-dup.mp4",
                FileSizeBytes = 10,
                DurationSeconds = 1
            });

            await db.SaveChangesAsync();
        }

        await using (var db = new AppDbContext(_dbOptions))
        {
            await StartupStorageMigration.RunAsync(db, _videoRoot, _screenshotRoot, NullLogger.Instance, _legacyRoot, default);
        }

        Assert.Multiple(() =>
        {
            Assert.That(File.Exists(newVideoPath), Is.True);
            Assert.That(File.Exists(oldVideoPath), Is.False);
        });
    }
}

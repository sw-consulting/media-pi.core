// Copyright (C) 2026 sw.consulting
// This file is a part of Media Pi backend

using System;
using System.IO;
using System.Threading;
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
        var markerPath = Path.Combine(markerDirectory, "storage-layout-v2.done");
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

    [Test]
    public async Task RunAsync_WhenCancelled_ThrowsAndDoesNotWriteMarker()
    {
        var videoRelative = "0001/video-cancel.mp4";
        var oldVideoPath = Path.Combine(_legacyRoot, videoRelative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(oldVideoPath)!);
        await File.WriteAllTextAsync(oldVideoPath, "video");

        await using (var db = new AppDbContext(_dbOptions))
        {
            db.Videos.Add(new Video
            {
                Id = 20,
                Title = "cancel-video",
                Filename = videoRelative,
                OriginalFilename = "video-cancel.mp4",
                FileSizeBytes = 5,
                DurationSeconds = 1
            });
            await db.SaveChangesAsync();
        }

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await using var db2 = new AppDbContext(_dbOptions);
        Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await StartupStorageMigration.RunAsync(db2, _videoRoot, _screenshotRoot, NullLogger.Instance, _legacyRoot, cts.Token));

        var markerPath = Path.Combine(_legacyRoot, ".migration", "storage-layout-v2.done");
        Assert.That(File.Exists(markerPath), Is.False, "Marker must not be written when the migration is cancelled");
    }

    [Test]
    public async Task RunAsync_WhenMoveFails_DoesNotWriteMarker()
    {
        var videoRelative = "0001/video-fail.mp4";
        var oldVideoPath = Path.Combine(_legacyRoot, videoRelative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(oldVideoPath)!);
        await File.WriteAllTextAsync(oldVideoPath, "video");

        // Block the target sub-directory by creating a regular file where a directory is expected.
        // Directory.CreateDirectory(_videoRoot/0001) will fail because '0001' already exists as a file.
        Directory.CreateDirectory(_videoRoot);
        await File.WriteAllTextAsync(Path.Combine(_videoRoot, "0001"), "blocking");

        await using (var db = new AppDbContext(_dbOptions))
        {
            db.Videos.Add(new Video
            {
                Id = 30,
                Title = "fail-video",
                Filename = videoRelative,
                OriginalFilename = "video-fail.mp4",
                FileSizeBytes = 5,
                DurationSeconds = 1
            });
            await db.SaveChangesAsync();
        }

        await using (var db = new AppDbContext(_dbOptions))
        {
            await StartupStorageMigration.RunAsync(db, _videoRoot, _screenshotRoot, NullLogger.Instance, _legacyRoot, default);
        }

        var markerPath = Path.Combine(_legacyRoot, ".migration", "storage-layout-v2.done");
        Assert.That(File.Exists(markerPath), Is.False, "Marker must not be written when file moves fail");
        Assert.That(File.Exists(oldVideoPath), Is.True, "File must remain in legacy root when its move fails");
    }

    // ── MoveFiles: same-root early return ────────────────────────────────────

    [Test]
    public async Task RunAsync_WhenVideoRootEqualsLegacyRoot_SkipsVideoMigration()
    {
        var videoRelative = "0041/video-same.mp4";
        var videoPath = Path.Combine(_legacyRoot, videoRelative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(videoPath)!);
        await File.WriteAllTextAsync(videoPath, "same-root-video");

        await using (var db = new AppDbContext(_dbOptions))
        {
            db.Videos.Add(new Video
            {
                Id = 40,
                Title = "same-root",
                Filename = videoRelative,
                OriginalFilename = "video-same.mp4",
                FileSizeBytes = 15,
                DurationSeconds = 1
            });
            await db.SaveChangesAsync();
        }

        // Pass _legacyRoot as videoRootPath so old == new root → MoveFiles early return
        await using (var db = new AppDbContext(_dbOptions))
        {
            await StartupStorageMigration.RunAsync(db, _legacyRoot, _screenshotRoot, NullLogger.Instance, _legacyRoot, default);
        }

        var markerPath = Path.Combine(_legacyRoot, ".migration", "storage-layout-v2.done");
        Assert.Multiple(() =>
        {
            Assert.That(File.Exists(markerPath), Is.True, "Marker should be written when same-root is skipped (no failures)");
            Assert.That(File.Exists(videoPath), Is.True, "File must stay in place when same-root migration is skipped");
        });
    }

    // ── MoveFiles: unsafe path traversal ─────────────────────────────────────

    [Test]
    public async Task RunAsync_WhenFilenameHasPathTraversal_SkipsUnsafeFile()
    {
        await using (var db = new AppDbContext(_dbOptions))
        {
            db.Videos.Add(new Video
            {
                Id = 50,
                Title = "unsafe",
                Filename = "../escape.mp4", // resolves outside _legacyRoot
                OriginalFilename = "escape.mp4",
                FileSizeBytes = 1,
                DurationSeconds = 1
            });
            await db.SaveChangesAsync();
        }

        await using (var db = new AppDbContext(_dbOptions))
        {
            await StartupStorageMigration.RunAsync(db, _videoRoot, _screenshotRoot, NullLogger.Instance, _legacyRoot, default);
        }

        // Escaped destination must not have been written to
        var escapedPath = Path.Combine(Path.GetDirectoryName(_legacyRoot)!, "escape.mp4");
        // No file should appear inside _videoRoot either (no successful move occurred)
        var movedPath = Path.Combine(_videoRoot, "escape.mp4");
        var markerPath = Path.Combine(_legacyRoot, ".migration", "storage-layout-v2.done");
        Assert.Multiple(() =>
        {
            Assert.That(File.Exists(escapedPath), Is.False, "Must not write outside the root boundary");
            Assert.That(File.Exists(movedPath), Is.False, "No file should appear in videoRoot for the unsafe path");
            Assert.That(File.Exists(markerPath), Is.True, "Marker must be written when the only file has an unsafe path (no failures)");
        });
    }

    // ── MoveFiles: destination already exists ─────────────────────────────────

    [Test]
    public async Task RunAsync_WhenNewFileAlreadyExistsAndOldExists_DeletesLegacyAndWritesMarker()
    {
        var videoRelative = "0060/video-both.mp4";
        var oldPath = Path.Combine(_legacyRoot, videoRelative.Replace('/', Path.DirectorySeparatorChar));
        var newPath = Path.Combine(_videoRoot, videoRelative.Replace('/', Path.DirectorySeparatorChar));

        Directory.CreateDirectory(Path.GetDirectoryName(oldPath)!);
        Directory.CreateDirectory(Path.GetDirectoryName(newPath)!);
        await File.WriteAllTextAsync(oldPath, "old-content");
        await File.WriteAllTextAsync(newPath, "new-content");

        await using (var db = new AppDbContext(_dbOptions))
        {
            db.Videos.Add(new Video
            {
                Id = 60,
                Title = "both-exist",
                Filename = videoRelative,
                OriginalFilename = "video-both.mp4",
                FileSizeBytes = 11,
                DurationSeconds = 1
            });
            await db.SaveChangesAsync();
        }

        await using (var db = new AppDbContext(_dbOptions))
        {
            await StartupStorageMigration.RunAsync(db, _videoRoot, _screenshotRoot, NullLogger.Instance, _legacyRoot, default);
        }

        var markerPath = Path.Combine(_legacyRoot, ".migration", "storage-layout-v2.done");
        Assert.Multiple(() =>
        {
            Assert.That(File.Exists(newPath), Is.True, "New file must remain");
            Assert.That(File.Exists(oldPath), Is.False, "Legacy file must be deleted when destination already exists");
            Assert.That(File.Exists(markerPath), Is.True, "Marker must be written");
        });
    }

    [Test]
    public async Task RunAsync_WhenNewFileAlreadyExistsButOldGone_ContinuesAndWritesMarker()
    {
        var videoRelative = "0070/video-new-only.mp4";
        var newPath = Path.Combine(_videoRoot, videoRelative.Replace('/', Path.DirectorySeparatorChar));
        // oldPath intentionally NOT created
        Directory.CreateDirectory(Path.GetDirectoryName(newPath)!);
        await File.WriteAllTextAsync(newPath, "destination-only");

        await using (var db = new AppDbContext(_dbOptions))
        {
            db.Videos.Add(new Video
            {
                Id = 70,
                Title = "new-only",
                Filename = videoRelative,
                OriginalFilename = "video-new-only.mp4",
                FileSizeBytes = 16,
                DurationSeconds = 1
            });
            await db.SaveChangesAsync();
        }

        await using (var db = new AppDbContext(_dbOptions))
        {
            await StartupStorageMigration.RunAsync(db, _videoRoot, _screenshotRoot, NullLogger.Instance, _legacyRoot, default);
        }

        var markerPath = Path.Combine(_legacyRoot, ".migration", "storage-layout-v2.done");
        Assert.Multiple(() =>
        {
            Assert.That(File.Exists(newPath), Is.True, "New file must remain");
            Assert.That(File.Exists(markerPath), Is.True, "Marker must be written");
        });
    }

    // ── MoveFiles: source file absent ────────────────────────────────────────

    [Test]
    public async Task RunAsync_WhenSourceFileAbsent_SkipsFileAndWritesMarker()
    {
        await using (var db = new AppDbContext(_dbOptions))
        {
            db.Videos.Add(new Video
            {
                Id = 80,
                Title = "missing",
                Filename = "0080/missing-video.mp4",
                OriginalFilename = "missing-video.mp4",
                FileSizeBytes = 5,
                DurationSeconds = 1
            });
            await db.SaveChangesAsync();
        }

        await using (var db = new AppDbContext(_dbOptions))
        {
            await StartupStorageMigration.RunAsync(db, _videoRoot, _screenshotRoot, NullLogger.Instance, _legacyRoot, default);
        }

        var markerPath = Path.Combine(_legacyRoot, ".migration", "storage-layout-v2.done");
        Assert.That(File.Exists(markerPath), Is.True, "Marker must be written when source file is absent (no failures)");
    }

    // ── WriteMarker: exception path ───────────────────────────────────────────

    [Test]
    public async Task RunAsync_WhenWriteMarkerDirectoryIsFile_CatchesExceptionAndCompletes()
    {
        // Create a regular FILE at the path where WriteMarker expects a directory;
        // Directory.CreateDirectory will throw and the exception is caught internally.
        var migrationPath = Path.Combine(_legacyRoot, ".migration");
        await File.WriteAllTextAsync(migrationPath, "blocking");

        // Empty DB → no migrations → failedVideos=0 → WriteMarker is called → fails silently
        await using (var db = new AppDbContext(_dbOptions))
        {
            await StartupStorageMigration.RunAsync(db, _videoRoot, _screenshotRoot, NullLogger.Instance, _legacyRoot, default);
        }

        // WriteMarker failed internally, so no marker file was written
        var markerPath = Path.Combine(_legacyRoot, ".migration", "storage-layout-v2.done");
        Assert.That(File.Exists(markerPath), Is.False, "Marker must NOT exist when WriteMarker throws internally");
    }

    // ── CleanupOldFilesWhenNewAlreadyExists: same-root early return ───────────

    [Test]
    public async Task RunAsync_WhenMarkerExistsAndVideoRootSameAsLegacy_SkipsVideoCleanup()
    {
        var markerDir = Path.Combine(_legacyRoot, ".migration");
        Directory.CreateDirectory(markerDir);
        await File.WriteAllTextAsync(
            Path.Combine(markerDir, "storage-layout-v2.done"), "done");

        await using (var db = new AppDbContext(_dbOptions))
        {
            db.Videos.Add(new Video
            {
                Id = 90,
                Title = "same-cleanup",
                Filename = "0090/video.mp4",
                OriginalFilename = "video.mp4",
                FileSizeBytes = 5,
                DurationSeconds = 1
            });
            await db.SaveChangesAsync();
        }

        // Pass _legacyRoot as videoRoot → cleanup sees same root → return 0 immediately
        await using (var db = new AppDbContext(_dbOptions))
        {
            await StartupStorageMigration.RunAsync(db, _legacyRoot, _screenshotRoot, NullLogger.Instance, _legacyRoot, default);
        }

        var markerPath = Path.Combine(markerDir, "storage-layout-v2.done");
        Assert.That(File.Exists(markerPath), Is.True, "Marker must remain after no-op cleanup");
    }

    // ── CleanupOldFilesWhenNewAlreadyExists: unsafe path ─────────────────────

    [Test]
    public async Task RunAsync_WhenMarkerExistsAndFilenameHasPathTraversal_SkipsUnsafeInCleanup()
    {
        var markerDir = Path.Combine(_legacyRoot, ".migration");
        Directory.CreateDirectory(markerDir);
        await File.WriteAllTextAsync(
            Path.Combine(markerDir, "storage-layout-v2.done"), "done");

        await using (var db = new AppDbContext(_dbOptions))
        {
            db.Videos.Add(new Video
            {
                Id = 100,
                Title = "unsafe-cleanup",
                Filename = "../escape-cleanup.mp4",
                OriginalFilename = "escape-cleanup.mp4",
                FileSizeBytes = 1,
                DurationSeconds = 1
            });
            await db.SaveChangesAsync();
        }

        await using (var db = new AppDbContext(_dbOptions))
        {
            await StartupStorageMigration.RunAsync(db, _videoRoot, _screenshotRoot, NullLogger.Instance, _legacyRoot, default);
        }

        // The escaped path must not have been touched during cleanup
        var escapedPath = Path.Combine(Path.GetDirectoryName(_legacyRoot)!, "escape-cleanup.mp4");
        var markerPath = Path.Combine(markerDir, "storage-layout-v2.done");
        Assert.Multiple(() =>
        {
            Assert.That(File.Exists(escapedPath), Is.False, "Must not touch files outside the root boundary during cleanup");
            Assert.That(File.Exists(markerPath), Is.True, "Marker must remain after skipping unsafe path in cleanup");
        });
    }

    // ── MoveFileWithFallback: IOException → CopyFsyncDelete failure ───────────

    [Test]
    public async Task RunAsync_WhenNewPathIsDirectory_FallsBackToCopyAndFails_NoMarker()
    {
        // Use a flat filename (no sub-directory) so targetDirectory = _videoRoot (already exists)
        var videoRelative = "video-xdev.mp4";
        var oldVideoPath = Path.Combine(_legacyRoot, videoRelative);
        await File.WriteAllTextAsync(oldVideoPath, "video-content");

        // Create a DIRECTORY at newPath so File.Move throws IOException (EISDIR on Linux)
        // and File.Copy to the same path also fails
        var newVideoPath = Path.Combine(_videoRoot, videoRelative);
        Directory.CreateDirectory(newVideoPath); // creates a dir named "video-xdev.mp4"

        await using (var db = new AppDbContext(_dbOptions))
        {
            db.Videos.Add(new Video
            {
                Id = 110,
                Title = "xdev",
                Filename = videoRelative,
                OriginalFilename = "video-xdev.mp4",
                FileSizeBytes = 13,
                DurationSeconds = 1
            });
            await db.SaveChangesAsync();
        }

        await using (var db = new AppDbContext(_dbOptions))
        {
            await StartupStorageMigration.RunAsync(db, _videoRoot, _screenshotRoot, NullLogger.Instance, _legacyRoot, default);
        }

        var markerPath = Path.Combine(_legacyRoot, ".migration", "storage-layout-v2.done");
        Assert.Multiple(() =>
        {
            Assert.That(File.Exists(markerPath), Is.False, "Marker must not be written when copy fallback fails");
            Assert.That(File.Exists(oldVideoPath), Is.True, "Source file must remain when move and copy both fail");
        });
    }

    // ── CopyFsyncDelete: direct tests (internal method) ──────────────────────

    [Test]
    public void CopyFsyncDelete_WhenPathsAreValid_CopiesAndDeletesSource()
    {
        var oldPath = Path.Combine(_legacyRoot, "source-copy.mp4");
        var newPath = Path.Combine(_legacyRoot, "dest-copy.mp4");
        File.WriteAllText(oldPath, "content-to-copy");

        var result = StartupStorageMigration.CopyFsyncDelete(
            oldPath, newPath, "source-copy.mp4", "video", NullLogger.Instance);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.True);
            Assert.That(File.ReadAllText(newPath), Is.EqualTo("content-to-copy"));
            Assert.That(File.Exists(oldPath), Is.False, "Source must be deleted after successful copy");
        });
    }

    [Test]
    public void CopyFsyncDelete_WhenDestinationAlreadyExists_CleansUpAndReturnsFalse()
    {
        var oldPath = Path.Combine(_legacyRoot, "source-fail.mp4");
        var newPath = Path.Combine(_legacyRoot, "existing-dest.mp4");
        File.WriteAllText(oldPath, "original");
        File.WriteAllText(newPath, "existing"); // overwrite:false → File.Copy throws

        var result = StartupStorageMigration.CopyFsyncDelete(
            oldPath, newPath, "source-fail.mp4", "video", NullLogger.Instance);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.False);
            Assert.That(File.Exists(newPath), Is.False, "Stale destination must be cleaned up after failed copy");
            Assert.That(File.Exists(oldPath), Is.True, "Source must remain when copy fails");
        });
    }

    // ── TryDeleteLegacyFile: failure path (direct test) ──────────────────────

    [Test]
    public void TryDeleteLegacyFile_WhenDeleteThrows_ReturnsFalse()
    {
        // File.Delete on a directory path throws (UnauthorizedAccessException on Linux via EISDIR→EACCES)
        var subDir = Path.Combine(_legacyRoot, "subdir-delete-test");
        Directory.CreateDirectory(subDir);

        var result = StartupStorageMigration.TryDeleteLegacyFile(
            subDir, "test/path.mp4", "video", NullLogger.Instance);

        Assert.That(result, Is.False, "TryDeleteLegacyFile must return false when File.Delete throws");
    }
}

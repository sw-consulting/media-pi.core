// Copyright (C) 2026 sw.consulting
// This file is a part of Media Pi backend

using MediaPi.Core.Data;
using Microsoft.EntityFrameworkCore;

namespace MediaPi.Core.Services;

public static class StartupStorageMigration
{
    public const string LegacySharedRoot = "/var/lib/storage";

    public static async Task RunAsync(
        AppDbContext db,
        string videoRootPath,
        string screenshotRootPath,
        ILogger logger,
        string? legacySharedRoot = null,
        CancellationToken ct = default)
    {
        var oldRootPath = string.IsNullOrWhiteSpace(legacySharedRoot) ? LegacySharedRoot : legacySharedRoot;

        Directory.CreateDirectory(videoRootPath);
        Directory.CreateDirectory(screenshotRootPath);

        var markerPath = Path.Combine(oldRootPath, ".migration", $"storage-layout-{VersionInfo.AppVersion}.done");

        if (File.Exists(markerPath))
        {
            var videoCleanup = await CleanupOldFilesWhenNewAlreadyExistsAsync(
                await db.Videos.AsNoTracking().Select(v => v.Filename).ToListAsync(ct),
                oldRootPath,
                videoRootPath,
                "video",
                logger);

            var screenshotCleanup = await CleanupOldFilesWhenNewAlreadyExistsAsync(
                await db.Screenshots.AsNoTracking().Select(s => s.Filename).ToListAsync(ct),
                oldRootPath,
                screenshotRootPath,
                "screenshot",
                logger);

            logger.LogInformation(
                "Storage migration marker found: {MarkerPath}. Cleanup done. Deleted legacy duplicates - videos: {Videos}, screenshots: {Screenshots}",
                markerPath,
                videoCleanup,
                screenshotCleanup);
            return;
        }

        var movedVideos = await MoveFilesAsync(
            await db.Videos.AsNoTracking().Select(v => v.Filename).ToListAsync(ct),
            oldRootPath,
            videoRootPath,
            "video",
            logger);

        var movedScreenshots = await MoveFilesAsync(
            await db.Screenshots.AsNoTracking().Select(s => s.Filename).ToListAsync(ct),
            oldRootPath,
            screenshotRootPath,
            "screenshot",
            logger);

        WriteMarker(markerPath, logger);

        logger.LogInformation(
            "Storage migration completed. Moved videos: {MovedVideos}, moved screenshots: {MovedScreenshots}",
            movedVideos,
            movedScreenshots);
    }

    private static void WriteMarker(string markerPath, ILogger logger)
    {
        try
        {
            var markerDirectory = Path.GetDirectoryName(markerPath);
            if (!string.IsNullOrWhiteSpace(markerDirectory))
            {
                Directory.CreateDirectory(markerDirectory);
            }

            File.WriteAllText(markerPath, $"Migrated at {DateTime.UtcNow:O}");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Storage migration marker could not be written: {MarkerPath}", markerPath);
        }
    }

    private static int MoveFiles(
        IEnumerable<string> filenames,
        string oldRootPath,
        string newRootPath,
        string kind,
        ILogger logger)
    {
        var oldRootFullPath = Path.GetFullPath(oldRootPath);
        var newRootFullPath = Path.GetFullPath(newRootPath);

        if (string.Equals(oldRootFullPath, newRootFullPath, StringComparison.Ordinal))
        {
            logger.LogInformation("Storage migration for {Kind} skipped: old and new roots are the same ({Root})", kind, oldRootFullPath);
            return 0;
        }

        var moved = 0;

        foreach (var filename in filenames.Where(name => !string.IsNullOrWhiteSpace(name)))
        {
            var relativePath = filename.Replace('/', Path.DirectorySeparatorChar);
            var oldPath = Path.GetFullPath(Path.Combine(oldRootFullPath, relativePath));
            var newPath = Path.GetFullPath(Path.Combine(newRootFullPath, relativePath));

            if (!IsPathInsideRoot(oldPath, oldRootFullPath) || !IsPathInsideRoot(newPath, newRootFullPath))
            {
                logger.LogWarning("Storage migration for {Kind} skipped unsafe path: {Filename}", kind, filename);
                continue;
            }

            if (File.Exists(newPath))
            {
                if (File.Exists(oldPath))
                {
                    TryDeleteLegacyFile(oldPath, filename, kind, logger);
                }
                continue;
            }

            if (!File.Exists(oldPath))
            {
                continue;
            }

            try
            {
                var targetDirectory = Path.GetDirectoryName(newPath);
                if (!string.IsNullOrWhiteSpace(targetDirectory))
                {
                    Directory.CreateDirectory(targetDirectory);
                }

                File.Move(oldPath, newPath);
                moved++;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Storage migration for {Kind} failed for file {Filename}", kind, filename);
            }
        }

        return moved;
    }

    private static int CleanupOldFilesWhenNewAlreadyExists(
        IEnumerable<string> filenames,
        string oldRootPath,
        string newRootPath,
        string kind,
        ILogger logger)
    {
        var oldRootFullPath = Path.GetFullPath(oldRootPath);
        var newRootFullPath = Path.GetFullPath(newRootPath);

        if (string.Equals(oldRootFullPath, newRootFullPath, StringComparison.Ordinal))
        {
            return 0;
        }

        var deleted = 0;
        foreach (var filename in filenames.Where(name => !string.IsNullOrWhiteSpace(name)))
        {
            var relativePath = filename.Replace('/', Path.DirectorySeparatorChar);
            var oldPath = Path.GetFullPath(Path.Combine(oldRootFullPath, relativePath));
            var newPath = Path.GetFullPath(Path.Combine(newRootFullPath, relativePath));

            if (!IsPathInsideRoot(oldPath, oldRootFullPath) || !IsPathInsideRoot(newPath, newRootFullPath))
            {
                continue;
            }

            if (File.Exists(newPath) && File.Exists(oldPath) && TryDeleteLegacyFile(oldPath, filename, kind, logger))
            {
                deleted++;
            }
        }

        return deleted;
    }

    private static bool TryDeleteLegacyFile(string oldPath, string filename, string kind, ILogger logger)
    {
        try
        {
            File.Delete(oldPath);
            logger.LogInformation("Deleted legacy {Kind} file after migration: {Filename}", kind, filename);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to delete legacy {Kind} file after migration: {Filename}", kind, filename);
            return false;
        }
    }

    private static Task<int> CleanupOldFilesWhenNewAlreadyExistsAsync(
        IEnumerable<string> filenames,
        string oldRootPath,
        string newRootPath,
        string kind,
        ILogger logger)
    {
        return Task.FromResult(CleanupOldFilesWhenNewAlreadyExists(filenames, oldRootPath, newRootPath, kind, logger));
    }

    private static Task<int> MoveFilesAsync(
        IEnumerable<string> filenames,
        string oldRootPath,
        string newRootPath,
        string kind,
        ILogger logger)
    {
        return Task.FromResult(MoveFiles(filenames, oldRootPath, newRootPath, kind, logger));
    }

    private static bool IsPathInsideRoot(string fullPath, string rootFullPath)
    {
        var rootWithSeparator = rootFullPath.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
            ? rootFullPath
            : rootFullPath + Path.DirectorySeparatorChar;

        return string.Equals(fullPath, rootFullPath, StringComparison.Ordinal)
            || fullPath.StartsWith(rootWithSeparator, StringComparison.Ordinal);
    }
}

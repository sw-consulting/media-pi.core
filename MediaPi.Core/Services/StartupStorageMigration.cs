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
            var videoCleanup = CleanupOldFilesWhenNewAlreadyExists(
                await db.Videos.AsNoTracking().Select(v => v.Filename).ToListAsync(ct),
                oldRootPath,
                videoRootPath,
                "video",
                logger,
                ct);

            var screenshotCleanup = CleanupOldFilesWhenNewAlreadyExists(
                await db.Screenshots.AsNoTracking().Select(s => s.Filename).ToListAsync(ct),
                oldRootPath,
                screenshotRootPath,
                "screenshot",
                logger,
                ct);

            logger.LogInformation(
                "Storage migration marker found: {MarkerPath}. Cleanup done. Deleted legacy duplicates - videos: {Videos}, screenshots: {Screenshots}",
                markerPath,
                videoCleanup,
                screenshotCleanup);
            return;
        }

        var (movedVideos, failedVideos) = MoveFiles(
            await db.Videos.AsNoTracking().Select(v => v.Filename).ToListAsync(ct),
            oldRootPath,
            videoRootPath,
            "video",
            logger,
            ct);

        var (movedScreenshots, failedScreenshots) = MoveFiles(
            await db.Screenshots.AsNoTracking().Select(s => s.Filename).ToListAsync(ct),
            oldRootPath,
            screenshotRootPath,
            "screenshot",
            logger,
            ct);

        if (failedVideos == 0 && failedScreenshots == 0)
        {
            WriteMarker(markerPath, logger);
            logger.LogInformation(
                "Storage migration completed. Moved videos: {MovedVideos}, moved screenshots: {MovedScreenshots}",
                movedVideos,
                movedScreenshots);
        }
        else
        {
            logger.LogWarning(
                "Storage migration completed with failures; marker not written. Videos: {MovedVideos} moved, {FailedVideos} failed. Screenshots: {MovedScreenshots} moved, {FailedScreenshots} failed.",
                movedVideos, failedVideos, movedScreenshots, failedScreenshots);
        }
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

    private static (int moved, int failed) MoveFiles(
        IEnumerable<string> filenames,
        string oldRootPath,
        string newRootPath,
        string kind,
        ILogger logger,
        CancellationToken ct)
    {
        var oldRootFullPath = Path.GetFullPath(oldRootPath);
        var newRootFullPath = Path.GetFullPath(newRootPath);

        if (string.Equals(oldRootFullPath, newRootFullPath, StringComparison.Ordinal))
        {
            logger.LogInformation("Storage migration for {Kind} skipped: old and new roots are the same ({Root})", kind, oldRootFullPath);
            return (0, 0);
        }

        var moved = 0;
        var failed = 0;

        foreach (var filename in filenames.Where(name => !string.IsNullOrWhiteSpace(name)))
        {
            ct.ThrowIfCancellationRequested();

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

                if (MoveFileWithFallback(oldPath, newPath, filename, kind, logger))
                {
                    moved++;
                }
                else
                {
                    failed++;
                }
            }
            catch (Exception ex)
            {
                failed++;
                logger.LogWarning(ex, "Storage migration for {Kind} failed for file {Filename}", kind, filename);
            }
        }

        return (moved, failed);
    }

    private static bool MoveFileWithFallback(string oldPath, string newPath, string filename, string kind, ILogger logger)
    {
        try
        {
            File.Move(oldPath, newPath);
            return true;
        }
        catch (IOException)
        {
            // File.Move fails for cross-device/cross-filesystem moves; fall back to copy+fsync+delete
            return CopyFsyncDelete(oldPath, newPath, filename, kind, logger);
        }
    }

    private static bool CopyFsyncDelete(string oldPath, string newPath, string filename, string kind, ILogger logger)
    {
        logger.LogDebug("File.Move failed for {Kind} file {Filename}; using copy+delete fallback", kind, filename);
        try
        {
            File.Copy(oldPath, newPath, overwrite: false);
            using var fs = new FileStream(newPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            fs.Flush(flushToDisk: true);
        }
        catch (Exception ex)
        {
            if (File.Exists(newPath))
            {
                try { File.Delete(newPath); } catch { /* best-effort: leaving a stale partial copy is preferable to masking the real copy failure */ }
            }
            logger.LogWarning(ex, "Storage migration copy+delete for {Kind} failed for file {Filename}", kind, filename);
            return false;
        }

        try
        {
            File.Delete(oldPath);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not delete legacy {Kind} file {Filename} after copy; will retry cleanup on next startup", kind, filename);
        }

        return true;
    }

    private static int CleanupOldFilesWhenNewAlreadyExists(
        IEnumerable<string> filenames,
        string oldRootPath,
        string newRootPath,
        string kind,
        ILogger logger,
        CancellationToken ct)
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
            ct.ThrowIfCancellationRequested();

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

    private static bool IsPathInsideRoot(string fullPath, string rootFullPath)
    {
        var rootWithSeparator = rootFullPath.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
            ? rootFullPath
            : rootFullPath + Path.DirectorySeparatorChar;

        return string.Equals(fullPath, rootFullPath, StringComparison.Ordinal)
            || fullPath.StartsWith(rootWithSeparator, StringComparison.Ordinal);
    }
}

// Copyright (C) 2025-2026 sw.consulting
// This file is a part of Media Pi backend

using MediaPi.Core.Authorization;
using MediaPi.Core.Data;
using MediaPi.Core.Models;
using MediaPi.Core.RestModels;
using MediaPi.Core.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

namespace MediaPi.Core.Controllers;

[ApiController]
[AuthorizeDevice]
[Route("api/[controller]")]
[Produces("application/json")]
[ProducesResponseType(StatusCodes.Status401Unauthorized, Type = typeof(ErrMessage))]
public class DeviceSyncController(
    IHttpContextAccessor httpContextAccessor,
    IVideoStorageService videoStorageService,
    IScreenshotStorageService screenshotStorageService,
    AppDbContext db,
    ILogger<DeviceSyncController> logger) : MediaPiControllerPreBase(db, logger)
{
    private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;
    private readonly IVideoStorageService _videoStorageService = videoStorageService;
    private readonly IScreenshotStorageService _screenshotStorageService = screenshotStorageService;

    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IEnumerable<DeviceSyncManifestItem>))]
    [ProducesResponseType(StatusCodes.Status500InternalServerError, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    public async Task<ActionResult<IEnumerable<DeviceSyncManifestItem>>> GetManifest(CancellationToken ct = default)
    {
        if (_httpContextAccessor.HttpContext?.Items["DeviceId"] is not int deviceId)
        {
            return _500DeviceIdMissing();
        }

        var device = await _db.Devices.AsNoTracking().FirstOrDefaultAsync(d => d.Id == deviceId, ct);
        if (device == null) return _404Device(deviceId);

        if (!device.DeviceGroupId.HasValue)
        {
            return new List<DeviceSyncManifestItem>();
        }

        var deviceGroupId = device.DeviceGroupId.Value;

        var videos = await _db.VideoPlaylists.AsNoTracking()
            .Where(vp => vp.Playlist.PlaylistDeviceGroups.Any(pdg => pdg.DeviceGroupId == deviceGroupId))
            .Select(vp => new
            {
                vp.Video.Id,
                vp.Video.Filename,
                vp.Video.FileSizeBytes,
                vp.Video.Sha256
            })
            // Videos can appear in multiple playlists; group by Video.Id to deduplicate per video.
            .GroupBy(video => video.Id)
            .Select(g => g.First())
            .ToListAsync(ct);

        // Despite Video.Filename being marked as 'required', we validate it here as a safety net
        // against potential data integrity issues (e.g., database migration problems, manual data edits).
        var missingFilename = videos.FirstOrDefault(video => string.IsNullOrWhiteSpace(video.Filename));
        if (missingFilename != null)
        {
            return _500VideoManifestFieldMissing(missingFilename.Id, "filename");
        }

        // If SHA256 is missing, calculate it on-the-fly (graceful fallback)
        var manifestItems = new List<DeviceSyncManifestItem>();
        var videosNeedingSha256Update = new List<(int VideoId, string CalculatedSha256)>();

        foreach (var video in videos)
        {
            var sha256 = video.Sha256;
            if (string.IsNullOrWhiteSpace(sha256))
            {
                sha256 = await CalculateSha256OnTheFlyAsync(video.Filename, video.Id, ct);
                if (sha256 == null)
                {
                    return _500VideoManifestFieldMissing(video.Id, "sha256 (on-the-fly calculation failed)");
                }

                // Track videos that need their SHA256 saved to the database
                videosNeedingSha256Update.Add((video.Id, sha256));
            }

            manifestItems.Add(new DeviceSyncManifestItem
            {
                Id = video.Id,
                Filename = video.Filename,
                FileSizeBytes = video.FileSizeBytes,
                Sha256 = sha256!
            });
        }

        // Save calculated SHA256 values to the database
        if (videosNeedingSha256Update.Count > 0)
        {
            await SaveSha256ToDatabaseAsync(videosNeedingSha256Update, ct);
        }

        return manifestItems;
    }

    /// <summary>
    /// Persists calculated SHA256 values to the database for videos that were missing them.
    /// </summary>
    private async Task SaveSha256ToDatabaseAsync(List<(int VideoId, string Sha256)> videoUpdates, CancellationToken ct)
    {
        try
        {
            foreach (var (videoId, sha256) in videoUpdates)
            {
                var video = await _db.Videos.FindAsync(new object[] { videoId }, cancellationToken: ct);
                if (video != null)
                {
                    video.Sha256 = sha256;
                    _logger.LogInformation("Updated SHA256 for Video ID: {VideoId}", videoId);
                }
            }

            if (videoUpdates.Count > 0)
            {
                await _db.SaveChangesAsync(ct);
                _logger.LogInformation("Saved SHA256 values for {Count} videos to the database", videoUpdates.Count);
            }
        }
        catch (Exception ex)
        {
            // Log but don't throw - the manifest was already generated successfully
            _logger.LogError(ex, "Failed to save calculated SHA256 values to the database for {Count} videos", videoUpdates.Count);
        }
    }

    /// <summary>
    /// Calculates SHA256 hash on-the-fly for videos missing the hash.
    /// This is a graceful fallback for data consistency issues.
    /// </summary>
    private async Task<string?> CalculateSha256OnTheFlyAsync(string relativeFilename, int videoId, CancellationToken ct)
    {
        try
        {
            var absolutePath = _videoStorageService.GetAbsolutePath(relativeFilename);
            if (!System.IO.File.Exists(absolutePath))
            {
                _logger.LogError("Video file not found for on-the-fly SHA256 calculation: {FilePath} (Video ID: {VideoId})", absolutePath, videoId);
                return null;
            }

            using var fileStream = new FileStream(absolutePath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
            using var sha256 = SHA256.Create();
            var buffer = new byte[81920];
            int bytesRead;
            while ((bytesRead = await fileStream.ReadAsync(buffer, ct)) > 0)
            {
                sha256.TransformBlock(buffer, 0, bytesRead, null, 0);
            }
            sha256.TransformFinalBlock(buffer, 0, 0);

            var hash = Convert.ToHexString(sha256.Hash ?? Array.Empty<byte>()).ToLowerInvariant();
            _logger.LogInformation("Calculated SHA256 on-the-fly for Video ID: {VideoId}: {Sha256}", videoId, hash);
            return hash;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to calculate SHA256 on-the-fly for Video ID: {VideoId}", videoId);
            return null;
        }
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    public async Task<IActionResult> Download(int id, CancellationToken ct = default)
    {
        if (_httpContextAccessor.HttpContext?.Items["DeviceId"] is not int deviceId)
        {
            return _500DeviceIdMissing();
        }

        var video = await _db.Videos.AsNoTracking().FirstOrDefaultAsync(v => v.Id == id, ct);
        if (video == null) return _404Video(id);

        var device = await _db.Devices.AsNoTracking().FirstOrDefaultAsync(d => d.Id == deviceId, ct);
        if (device == null) return _404Device(deviceId);

        // Verify that the video belongs to a playlist assigned to the device's group
        // If device has no group, authorization fails without a database query
        bool isAuthorized;
        if (!device.DeviceGroupId.HasValue)
        {
            isAuthorized = false;
        }
        else
        {
            isAuthorized = await _db.VideoPlaylists.AsNoTracking()
                .AnyAsync(vp => vp.VideoId == id && 
                               vp.Playlist.PlaylistDeviceGroups.Any(pdg => pdg.DeviceGroupId == device.DeviceGroupId.Value), ct);
        }

        if (!isAuthorized)
        {
            return _403DeviceUnauthorizedVideo(deviceId, id);
        }

        var path = _videoStorageService.GetAbsolutePath(video.Filename);
        return PhysicalFile(path, "application/octet-stream", video.OriginalFilename);
    }

    [HttpGet("playlist")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    public async Task<IActionResult> DownloadPlaylist(CancellationToken ct = default)
    {
        if (_httpContextAccessor.HttpContext?.Items["DeviceId"] is not int deviceId)
        {
            return _500DeviceIdMissing();
        }

        var device = await _db.Devices.AsNoTracking().FirstOrDefaultAsync(d => d.Id == deviceId, ct);
        if (device == null) return _404Device(deviceId);

        if (!device.DeviceGroupId.HasValue)
        {
            return NoContent();
        }

        var deviceGroupId = device.DeviceGroupId.Value;

        // Find a playlist with Play=true associated with this device group
        var playlistWithPlay = await _db.PlaylistDeviceGroups.AsNoTracking()
            .Include(pdg => pdg.Playlist)
                .ThenInclude(p => p.VideosPlaylist)
                    .ThenInclude(vp => vp.Video)
            .Where(pdg => pdg.DeviceGroupId == deviceGroupId && pdg.Play == true)
            .FirstOrDefaultAsync(ct);

        if (playlistWithPlay == null)
        {
            return NoContent();
        }

        // Get videos ordered by position
        var videos = playlistWithPlay.Playlist.VideosPlaylist
            .OrderBy(vp => vp.Position)
            .Select(vp => vp.Video.Filename)
            .ToList();

        // Generate M3U content
        var m3uContent = GenerateM3uContent(videos);

        // Return as text/plain with .m3u extension
        var bytes = System.Text.Encoding.UTF8.GetBytes(m3uContent);
        return File(bytes, "text/plain", "playlist.m3u");
    }

    private static string GenerateM3uContent(List<string> videoFilenames)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("#EXTM3U");

        foreach (var filename in videoFilenames)
        {
            sb.AppendLine(filename);
        }

        return sb.ToString();
    }

    [HttpPost("screenshot")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(Reference))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status500InternalServerError, Type = typeof(ErrMessage))]
    public async Task<ActionResult<Reference>> UploadScreenshot([FromForm] IFormFile file, CancellationToken ct = default)
    {
        if (_httpContextAccessor.HttpContext?.Items["DeviceId"] is not int deviceId)
        {
            return _500DeviceIdMissing();
        }

        var device = await _db.Devices.FirstOrDefaultAsync(d => d.Id == deviceId, ct);
        if (device == null)
        {
            return _404Device(deviceId);
        }

        if (file == null || file.Length == 0)
        {
            return _400ScreenshotFileMissing();
        }

        var saveResult = await _screenshotStorageService.SaveScreenshotAsync(file, file.FileName, ct);

        var screenshot = new Screenshot
        {
            Filename = saveResult.Filename,
            OriginalFilename = saveResult.OriginalFilename,
            FileSizeBytes = saveResult.FileSizeBytes,
            TimeCreated = saveResult.TimeCreated,
            DeviceId = deviceId
        };

        _db.Screenshots.Add(screenshot);
        await _db.SaveChangesAsync(ct);

        return StatusCode(StatusCodes.Status201Created, new Reference { Id = screenshot.Id });
    }
}

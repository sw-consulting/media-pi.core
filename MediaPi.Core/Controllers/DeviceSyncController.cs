// Copyright (c) 2025 sw.consulting
// This file is a part of Media Pi backend

using MediaPi.Core.Authorization;
using MediaPi.Core.Data;
using MediaPi.Core.RestModels;
using MediaPi.Core.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MediaPi.Core.Controllers;

[ApiController]
[AuthorizeDevice]
[Route("api/[controller]")]
[Produces("application/json")]
[ProducesResponseType(StatusCodes.Status401Unauthorized, Type = typeof(ErrMessage))]
public class DeviceSyncController(
    IHttpContextAccessor httpContextAccessor,
    IVideoStorageService videoStorageService,
    AppDbContext db,
    ILogger<DeviceSyncController> logger) : MediaPiControllerPreBase(db, logger)
{
    private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;
    private readonly IVideoStorageService _videoStorageService = videoStorageService;

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

        var missingSha256 = videos.FirstOrDefault(video => string.IsNullOrWhiteSpace(video.Sha256));
        if (missingSha256 != null)
        {
            return _500VideoManifestFieldMissing(missingSha256.Id, "sha256");
        }

        var manifest = videos.Select(video => new DeviceSyncManifestItem
        {
            Id = video.Id,
            Filename = video.Filename,
            FileSizeBytes = video.FileSizeBytes,
            Sha256 = video.Sha256! // Safe after validation above
        }).ToList();

        return manifest;
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

    private string GenerateM3uContent(List<string> videoFilenames)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("#EXTM3U");

        foreach (var filename in videoFilenames)
        {
            sb.AppendLine(filename);
        }

        return sb.ToString();
    }
}

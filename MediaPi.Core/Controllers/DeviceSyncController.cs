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
    ILogger<DeviceSyncController> logger) : FuelfluxControllerPreBase(db, logger)
{
    private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;
    private readonly IVideoStorageService _videoStorageService = videoStorageService;

    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IEnumerable<DeviceSyncManifestItem>))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    public async Task<ActionResult<IEnumerable<DeviceSyncManifestItem>>> GetManifest(CancellationToken ct = default)
    {
        var deviceId = (int)_httpContextAccessor.HttpContext!.Items["DeviceId"]!;

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
            Sha256 = video.Sha256
        }).ToList();

        return manifest;
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    public async Task<IActionResult> Download(int id, CancellationToken ct = default)
    {
        var video = await _db.Videos.AsNoTracking().FirstOrDefaultAsync(v => v.Id == id, ct);
        if (video == null) return _404Video(id);

        var path = _videoStorageService.GetAbsolutePath(video.Filename);
        return PhysicalFile(path, "application/octet-stream", video.OriginalFilename);
    }
}

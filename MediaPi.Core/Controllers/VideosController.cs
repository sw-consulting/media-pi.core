// Copyright (c) 2025 sw.consulting
// This file is a part of Media Pi backend

using System.Collections.Generic;
using System.Linq;

using MediaPi.Core.Authorization;
using MediaPi.Core.Data;
using MediaPi.Core.Extensions;
using MediaPi.Core.Models;
using MediaPi.Core.RestModels;
using MediaPi.Core.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MediaPi.Core.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
[Produces("application/json")]
[ProducesResponseType(StatusCodes.Status401Unauthorized, Type = typeof(ErrMessage))]
public class VideosController(
    IHttpContextAccessor httpContextAccessor,
    IUserInformationService userInformationService,
    IVideoStorageService videoStorageService,
    AppDbContext db,
    ILogger<VideosController> logger) : MediaPiControllerBase(httpContextAccessor, db, logger)
{
    private readonly IUserInformationService _userInformationService = userInformationService;
    private readonly IVideoStorageService _videoStorageService = videoStorageService;

    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IEnumerable<VideoViewItem>))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    public async Task<ActionResult<IEnumerable<VideoViewItem>>> GetVideos(CancellationToken ct = default)
    {
        var user = await CurrentUser();
        if (user == null) return _403();

        IQueryable<Video> query = _db.Videos.AsNoTracking();

        if (user.IsAdministrator())
        {
            // Administrators can see all videos; no filtering is applied.
        }
        else if (user.IsManager())
        {
            var accountIds = _userInformationService.GetUserAccountIds(user);
            query = query.Where(v => accountIds.Contains(v.AccountId));
        }
        else
        {
            return _403();
        }

        var videos = await query.ToListAsync(ct);
        return videos.Select(v => v.ToViewItem()).ToList();
    }

    [HttpGet("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(VideoViewItem))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    public async Task<ActionResult<VideoViewItem>> GetVideo(int id, CancellationToken ct = default)
    {
        var user = await CurrentUser();
        if (user == null) return _403();

        var video = await _db.Videos.AsNoTracking().FirstOrDefaultAsync(v => v.Id == id, ct);
        if (video == null) return _404Video(id);

        if (!_userInformationService.UserCanManageAccount(user, video.AccountId)) return _403();

        return video.ToViewItem();
    }

    [HttpPost("upload")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(Reference))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    public async Task<ActionResult<Reference>> UploadVideo([FromForm] VideoUploadItem item, CancellationToken ct = default)
    {
        var user = await CurrentUser();
        if (user == null) return _403();

        if (item.File == null || item.File.Length == 0) return _400VideoFileMissing();
        if (string.IsNullOrWhiteSpace(item.Title)) return _400VideoTitleMissing();

        var account = await _db.Accounts.FindAsync([item.AccountId], ct);
        if (account == null) return _404Account(item.AccountId);

        if (!_userInformationService.UserCanManageAccount(user, item.AccountId)) return _403();

        var storedFilename = await _videoStorageService.SaveVideoAsync(item.File, item.Title, ct);

        var video = new Video
        {
            Title = item.Title,
            Filename = storedFilename,
            AccountId = item.AccountId,
        };

        _db.Videos.Add(video);
        await _db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetVideo), new { id = video.Id }, new Reference { Id = video.Id });
    }

    [HttpPut("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    public async Task<IActionResult> UpdateVideo(int id, VideoUpdateItem item, CancellationToken ct = default)
    {
        var user = await CurrentUser();
        if (user == null) return _403();

        if (string.IsNullOrWhiteSpace(item.Title)) return _400VideoTitleMissing();

        var video = await _db.Videos
            .Include(v => v.VideoPlaylists)
            .FirstOrDefaultAsync(v => v.Id == id, ct);
        if (video == null) return _404Video(id);

        if (!_userInformationService.UserCanManageAccount(user, video.AccountId)) return _403();

        video.Title = item.Title;

        if (item.PlaylistIds != null)
        {
            var (playlistIds, validationError) = await ValidateVideoPlaylists(item.PlaylistIds, video.AccountId, ct);
            if (validationError != null) return validationError;

            ApplyVideoPlaylists(video, playlistIds);
        }

        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    private async Task<(List<int> PlaylistIds, ObjectResult? Error)> ValidateVideoPlaylists(IEnumerable<int> playlistIds, int accountId, CancellationToken ct)
    {
        var normalized = (playlistIds ?? Enumerable.Empty<int>()).Distinct().ToList();
        if (normalized.Count == 0) return (normalized, null);

        var playlists = await _db.Playlists
            .AsNoTracking()
            .Where(p => normalized.Contains(p.Id))
            .Select(p => new { p.Id, p.AccountId })
            .ToListAsync(ct);

        var foundIds = playlists.Select(p => p.Id).ToHashSet();
        if (foundIds.Count != normalized.Count)
        {
            var missingId = normalized.Except(foundIds).First();
            return (normalized, _404Playlist(missingId));
        }

        var mismatch = playlists.FirstOrDefault(p => p.AccountId != accountId);
        if (mismatch != null)
        {
            return (normalized, _400VideoPlaylistAccountMismatch(mismatch.Id, accountId));
        }

        return (normalized, null);
    }

    private void ApplyVideoPlaylists(Video video, IReadOnlyCollection<int> playlistIds)
    {
        var desired = playlistIds.ToHashSet();

        var toRemove = video.VideoPlaylists.Where(vp => !desired.Contains(vp.PlaylistId)).ToList();
        if (toRemove.Count != 0)
        {
            foreach (var remove in toRemove)
            {
                video.VideoPlaylists.Remove(remove);
            }
        }

        var existing = video.VideoPlaylists.Select(vp => vp.PlaylistId).ToHashSet();
        foreach (var playlistId in desired.Except(existing))
        {
            video.VideoPlaylists.Add(new VideoPlaylist { VideoId = video.Id, PlaylistId = playlistId });
        }
    }

    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    public async Task<IActionResult> DeleteVideo(int id, CancellationToken ct = default)
    {
        var user = await CurrentUser();
        if (user == null) return _403();

        var video = await _db.Videos
            .Include(v => v.VideoPlaylists)
            .FirstOrDefaultAsync(v => v.Id == id, ct);
        if (video == null) return _404Video(id);

        if (!_userInformationService.UserCanManageAccount(user, video.AccountId)) return _403();

        if (video.VideoPlaylists.Count != 0)
        {
            _db.VideoPlaylists.RemoveRange(video.VideoPlaylists);
        }

        _db.Videos.Remove(video);
        await _db.SaveChangesAsync(ct);

        await _videoStorageService.DeleteVideoAsync(video.Filename, ct);

        return NoContent();
    }
}

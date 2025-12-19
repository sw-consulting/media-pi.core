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
public class PlaylistsController(
    IHttpContextAccessor httpContextAccessor,
    IUserInformationService userInformationService,
    AppDbContext db,
    ILogger<PlaylistsController> logger) : MediaPiControllerBase(httpContextAccessor, db, logger)
{
    private readonly IUserInformationService _userInformationService = userInformationService;

    // GET: api/playlists
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IEnumerable<PlaylistViewItem>))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    public async Task<ActionResult<IEnumerable<PlaylistViewItem>>> GetPlaylists(CancellationToken ct = default)
    {
        var user = await CurrentUser();
        if (user == null) return _403();

        IQueryable<Playlist> query = _db.Playlists
            .AsNoTracking()
            .Include(p => p.VideoPlaylists)
                .ThenInclude(vp => vp.Video);

        if (user.IsAdministrator())
        {
            // all playlists
        }
        else if (user.IsManager())
        {
            var accountIds = _userInformationService.GetUserAccountIds(user);
            query = query.Where(p => accountIds.Contains(p.AccountId));
        }
        else
        {
            return _403();
        }

        var playlists = await query.ToListAsync(ct);
        return playlists.Select(p => p.ToViewItem()).ToList();
    }

    // GET: api/playlists/by-account/{accountId}
    [HttpGet("by-account/{accountId}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IEnumerable<PlaylistViewItem>))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    public async Task<ActionResult<IEnumerable<PlaylistViewItem>>> GetPlaylistsByAccount(int accountId, CancellationToken ct = default)
    {
        var user = await CurrentUser();
        if (user == null) return _403();

        if (!_userInformationService.UserCanManageAccount(user, accountId)) return _403();

        var playlists = await _db.Playlists
            .AsNoTracking()
            .Where(d => d.AccountId == accountId)
            .Include(p => p.VideoPlaylists)
                .ThenInclude(vp => vp.Video)
            .ToListAsync(ct);

        return playlists.Select(v => v.ToViewItem()).ToList();
    }

    // GET: api/playlists/{id}
    [HttpGet("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(PlaylistViewItem))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    public async Task<ActionResult<PlaylistViewItem>> GetPlaylist(int id, CancellationToken ct = default)
    {
        var user = await CurrentUser();
        if (user == null) return _403();

        var playlist = await _db.Playlists
            .AsNoTracking()
            .Include(p => p.VideoPlaylists)
                .ThenInclude(vp => vp.Video)
            .FirstOrDefaultAsync(p => p.Id == id, ct);
        if (playlist == null) return _404Playlist(id);

        if (!_userInformationService.UserCanManageAccount(user, playlist.AccountId)) return _403();

        return playlist.ToViewItem();
    }

    // POST: api/playlists
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(Reference))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status409Conflict, Type = typeof(ErrMessage))]
    public async Task<ActionResult<Reference>> CreatePlaylist(PlaylistCreateItem item, CancellationToken ct = default)
    {
        var user = await CurrentUser();
        if (user == null) return _403();

        var account = await _db.Accounts.FindAsync([item.AccountId], ct);
        if (account == null) return _404Account(item.AccountId);

        if (!_userInformationService.UserCanManageAccount(user, item.AccountId)) return _403();

        // Check for duplicate filename before creating playlist
        if (await _db.Playlists.AnyAsync(p => p.AccountId == item.AccountId && p.Filename == item.Filename, ct))
        {
            return _409PlaylistFilename(item.Filename);
        }

        // Allow empty playlists: treat missing/null items as empty list
        var items = item.Items ?? new List<PlaylistItemDto>();

        var (playlistVideoIds, itemValidationError) = await ValidatePlaylistItems(items, account.Id, ct);
        if (itemValidationError != null) return itemValidationError;

        var playlist = new Playlist
        {
            Title = item.Title,
            Filename = item.Filename,
            AccountId = item.AccountId,
        };

        // Add items with their positions
        foreach (var playlistItem in items.OrderBy(i => i.Position))
        {
            playlist.VideoPlaylists.Add(new VideoPlaylist 
            { 
                VideoId = playlistItem.VideoId, 
                Position = playlistItem.Position,
                Playlist = playlist 
            });
        }

        _db.Playlists.Add(playlist);
        await _db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetPlaylist), new { id = playlist.Id }, new Reference { Id = playlist.Id });
    }

    // PUT: api/playlists/{id}
    [HttpPut("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status409Conflict, Type = typeof(ErrMessage))]
    public async Task<IActionResult> UpdatePlaylist(int id, PlaylistUpdateItem item, CancellationToken ct = default)
    {
        var user = await CurrentUser();
        if (user == null) return _403();

        var playlist = await _db.Playlists
            .Include(p => p.VideoPlaylists)
                .ThenInclude(vp => vp.Video)
            .FirstOrDefaultAsync(p => p.Id == id, ct);
        if (playlist == null) return _404Playlist(id);

        if (!_userInformationService.UserCanManageAccount(user, playlist.AccountId)) return _403();

        // Check for duplicate filename before updating (exclude current playlist)
        if (await _db.Playlists.AnyAsync(p => p.AccountId == playlist.AccountId && p.Filename == item.Filename && p.Id != id, ct))
        {
            return _409PlaylistFilename(item.Filename);
        }

        playlist.UpdateFrom(item);

        // Allow empty playlists: treat missing/null items as empty list
        var items = item.Items ?? new List<PlaylistItemDto>();

        var (_, validationError) = await ValidatePlaylistItems(items, playlist.AccountId, ct);
        if (validationError != null) return validationError;

        // Remove all existing items and replace with new ones
        var toRemove = playlist.VideoPlaylists.ToList();
        if (toRemove.Count > 0)
        {
            playlist.VideoPlaylists.Clear();
            _db.VideoPlaylists.RemoveRange(toRemove);
        }

        // Add new items
        foreach (var playlistItem in items.OrderBy(i => i.Position))
        {
            playlist.VideoPlaylists.Add(new VideoPlaylist 
            { 
                PlaylistId = playlist.Id, 
                VideoId = playlistItem.VideoId,
                Position = playlistItem.Position
            });
        }

        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    // DELETE: api/playlists/{id}
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    public async Task<IActionResult> DeletePlaylist(int id, CancellationToken ct = default)
    {
        var user = await CurrentUser();
        if (user == null) return _403();

        var playlist = await _db.Playlists
            .Include(p => p.VideoPlaylists)
                .ThenInclude(vp => vp.Video)
            .FirstOrDefaultAsync(p => p.Id == id, ct);
        if (playlist == null) return _404Playlist(id);

        if (!_userInformationService.UserCanManageAccount(user, playlist.AccountId)) return _403();

        if (playlist.VideoPlaylists.Count != 0)
        {
            _db.VideoPlaylists.RemoveRange(playlist.VideoPlaylists);
        }

        _db.Playlists.Remove(playlist);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    private async Task<(List<int> VideoIds, ObjectResult? Error)> ValidatePlaylistItems(IEnumerable<PlaylistItemDto> items, int accountId, CancellationToken ct)
    {
        var videoIds = items.Select(i => i.VideoId).Distinct().ToList();
        
        if (videoIds.Count == 0) return (videoIds, null);

        // Validate positions
        var positions = items.Select(i => i.Position).ToList();
        if (positions.Any(p => p < 0))
        {
            return (videoIds, _400PlaylistItemPositionsNegative());
        }
        if (positions.Count != positions.Distinct().Count())
        {
            return (videoIds, _400PlaylistItemPositionsDuplicate());
        }

        var videos = await _db.Videos
            .AsNoTracking()
            .Where(v => videoIds.Contains(v.Id))
            .Select(v => new { v.Id, v.AccountId })
            .ToListAsync(ct);

        if (videos.Count != videoIds.Count)
        {
            var foundIds = videos.Select(v => v.Id).ToHashSet();
            var missingId = videoIds.First(id => !foundIds.Contains(id));
            return (videoIds, _404Video(missingId));
        }

        var mismatch = videos.FirstOrDefault(v => v.AccountId != null && v.AccountId != 0 && v.AccountId != accountId);
        if (mismatch != null)
        {
            return (videoIds, _400PlaylistVideoAccountMismatch(mismatch.Id, accountId));
        }

        return (videoIds, null);
    }
}

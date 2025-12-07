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
    public async Task<ActionResult<Reference>> CreatePlaylist(PlaylistCreateItem item, CancellationToken ct = default)
    {
        var user = await CurrentUser();
        if (user == null) return _403();

        var account = await _db.Accounts.FindAsync([item.AccountId], ct);
        if (account == null) return _404Account(item.AccountId);

        if (!_userInformationService.UserCanManageAccount(user, item.AccountId)) return _403();

        var (videoIds, validationError) = await ValidatePlaylistVideos(item.VideoIds, account.Id, ct);
        if (validationError != null) return validationError;

        // Handle both legacy VideoIds and new Items structure
        var playlistItems = GetPlaylistItems(item);
        var (playlistVideoIds, itemValidationError) = await ValidatePlaylistItems(playlistItems, account.Id, ct);
        if (itemValidationError != null) return itemValidationError;

        var playlist = new Playlist
        {
            Title = item.Title,
            Filename = item.Filename,
            AccountId = item.AccountId,
        };

        // Use Items if provided, otherwise fall back to VideoIds for backwards compatibility
        if (item.Items?.Count > 0)
        {
            foreach (var playlistItem in playlistItems.OrderBy(i => i.Position))
            {
                playlist.VideoPlaylists.Add(new VideoPlaylist 
                { 
                    VideoId = playlistItem.VideoId, 
                    Position = playlistItem.Position,
                    Playlist = playlist 
                });
            }
        }
        else
        {
            // Legacy support: assign sequential positions
            for (int i = 0; i < videoIds.Count; i++)
            {
                playlist.VideoPlaylists.Add(new VideoPlaylist 
                { 
                    VideoId = videoIds[i], 
                    Position = i,
                    Playlist = playlist 
                });
            }
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

        playlist.UpdateFrom(item);

        // Handle both legacy VideoIds and new Items structure
        if (item.Items?.Count > 0)
        {
            // Use new Items structure
            var playlistItems = GetPlaylistItems(item);
            var (_, validationError) = await ValidatePlaylistItems(playlistItems, playlist.AccountId, ct);
            if (validationError != null) return validationError;

            // Remove all existing items and replace with new ones
            var toRemove = playlist.VideoPlaylists.ToList();
            if (toRemove.Count > 0)
            {
                playlist.VideoPlaylists.Clear();
                _db.VideoPlaylists.RemoveRange(toRemove);
            }

            // Add new items
            foreach (var playlistItem in playlistItems.OrderBy(i => i.Position))
            {
                playlist.VideoPlaylists.Add(new VideoPlaylist 
                { 
                    PlaylistId = playlist.Id, 
                    VideoId = playlistItem.VideoId,
                    Position = playlistItem.Position
                });
            }
        }
        else if (item.VideoIds is not null)
        {
            // Legacy VideoIds support
            var (videoIds, validationError) = await ValidatePlaylistVideos(item.VideoIds, playlist.AccountId, ct);
            if (validationError != null) return validationError;

            var normalizedSet = videoIds.ToHashSet();
            var toRemove = playlist.VideoPlaylists.Where(vp => !normalizedSet.Contains(vp.VideoId)).ToList();
            foreach (var remove in toRemove)
            {
                playlist.VideoPlaylists.Remove(remove);
            }
            if (toRemove.Count > 0)
            {
                _db.VideoPlaylists.RemoveRange(toRemove);
            }

            var existingIds = playlist.VideoPlaylists.Select(vp => vp.VideoId).ToHashSet();
            var position = playlist.VideoPlaylists.Count > 0 ? playlist.VideoPlaylists.Max(vp => vp.Position) + 1 : 0;
            
            foreach (var videoId in normalizedSet.Except(existingIds))
            {
                playlist.VideoPlaylists.Add(new VideoPlaylist 
                { 
                    PlaylistId = playlist.Id, 
                    VideoId = videoId,
                    Position = position++
                });
             }
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

    private async Task<(List<int> VideoIds, ObjectResult? Error)> ValidatePlaylistVideos(IEnumerable<int>? videoIds, int accountId, CancellationToken ct)
    {
        var normalized = (videoIds ?? Enumerable.Empty<int>()).Distinct().ToList();
        if (normalized.Count == 0) return (normalized, null);

        var videos = await _db.Videos
            .AsNoTracking()
            .Where(v => normalized.Contains(v.Id))
            .Select(v => new { v.Id, v.AccountId })
            .ToListAsync(ct);

        if (videos.Count != normalized.Count)
        {
            var foundIds = videos.Select(v => v.Id).ToHashSet();
            var missingId = normalized.First(id => !foundIds.Contains(id));
            return (normalized, _404Video(missingId));
        }

        var mismatch = videos.FirstOrDefault(v => v.AccountId != accountId);
        if (mismatch != null)
        {
            return (normalized, _400PlaylistVideoAccountMismatch(mismatch.Id, accountId));
        }

        return (normalized, null);
    }

    private static List<PlaylistItemDto> GetPlaylistItems(PlaylistCreateItem item)
    {
        if (item.Items?.Count > 0)
        {
            return item.Items;
        }
        
        // Convert legacy VideoIds to Items with sequential positions
        return item.VideoIds.Select((videoId, index) => new PlaylistItemDto 
        { 
            VideoId = videoId, 
            Position = index 
        }).ToList();
    }

    private static List<PlaylistItemDto> GetPlaylistItems(PlaylistUpdateItem item)
    {
        if (item.Items?.Count > 0)
        {
            return item.Items;
        }
        
        // Convert legacy VideoIds to Items with sequential positions
        return item.VideoIds?.Select((videoId, index) => new PlaylistItemDto 
        { 
            VideoId = videoId, 
            Position = index 
        }).ToList() ?? [];
    }

    private async Task<(List<int> VideoIds, ObjectResult? Error)> ValidatePlaylistItems(IEnumerable<PlaylistItemDto> items, int accountId, CancellationToken ct)
    {
        var videoIds = items.Select(i => i.VideoId).Distinct().ToList();
        
        if (videoIds.Count == 0) return (videoIds, null);

        // Validate positions are sequential and start from 0
        var positions = items.Select(i => i.Position).ToList();
        if (positions.Any(p => p < 0))
        {
            return (videoIds, BadRequest(new ErrMessage { Msg = "Playlist item positions must be non-negative" }));
        }
        if (positions.Count != positions.Distinct().Count())
        {
            return (videoIds, BadRequest(new ErrMessage { Msg = "Playlist item positions must be unique" }));
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

        var mismatch = videos.FirstOrDefault(v => v.AccountId != accountId);
        if (mismatch != null)
        {
            return (videoIds, _400PlaylistVideoAccountMismatch(mismatch.Id, accountId));
        }

        return (videoIds, null);
    }
}

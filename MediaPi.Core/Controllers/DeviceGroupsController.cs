// Copyright (c) 2025 sw.consulting
// This file is a part of Media Pi backend

using MediaPi.Core.Authorization;
using MediaPi.Core.Data;
using MediaPi.Core.Extensions;
using MediaPi.Core.Models;
using MediaPi.Core.RestModels;
using MediaPi.Core.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MediaPi.Core.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
[Produces("application/json")]
[ProducesResponseType(StatusCodes.Status401Unauthorized, Type = typeof(ErrMessage))]
public class DeviceGroupsController(
    IHttpContextAccessor httpContextAccessor,
    IUserInformationService userInformationService,
    AppDbContext db,
    ILogger<DeviceGroupsController> logger) : MediaPiControllerBase(httpContextAccessor, db, logger)
{
    // GET: api/devicegroups
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IEnumerable<DeviceGroupViewItem>))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    public async Task<ActionResult<IEnumerable<DeviceGroupViewItem>>> GetAll(CancellationToken ct = default)
    {
        var user = await CurrentUser();
        if (user == null) return _403();

        IQueryable<DeviceGroup> query = _db.DeviceGroups;
        if (user.IsAdministrator())
        {
            // all groups
        }
        else if (user.IsManager())
        {
            var accountIds = userInformationService.GetUserAccountIds(user);
            query = query.Where(g => accountIds.Contains(g.AccountId));
        }
        else
        {
            return _403();
        }

        var groups = await query.ToListAsync(ct);
        return groups.Select(g => g.ToViewItem()).ToList();
    }

    // GET: api/devicegroups/by-account/{accountId?}
    [HttpGet("by-account/{accountId?}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IEnumerable<DeviceGroupViewItem>))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    public async Task<ActionResult<IEnumerable<DeviceGroupViewItem>>> GetAllByAccount(int? accountId, CancellationToken ct = default)
    {
        var user = await CurrentUser();
        if (user == null) return _403();

        IQueryable<DeviceGroup> query = _db.DeviceGroups;
        if (user.IsAdministrator())
        {
            if (accountId != null)
            {
                query = query.Where(g => g.AccountId == accountId.Value);
            }
            // else all groups
        }
        else if (user.IsManager())
        {
            if (accountId == null) return _403();
            var accountIds = userInformationService.GetUserAccountIds(user);
            if (!accountIds.Contains(accountId.Value)) return _403();
            query = query.Where(g => g.AccountId == accountId.Value);
        }
        else
        {
            return _403();
        }

        var groups = await query.ToListAsync(ct);
        return groups.Select(g => g.ToViewItem()).ToList();
    }

    // GET: api/devicegroups/{id}
    [HttpGet("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(DeviceGroupViewItem))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    public async Task<ActionResult<DeviceGroupViewItem>> GetGroup(int id, CancellationToken ct = default)
    {
        var user = await CurrentUser();
        if (user == null) return _403();

        var group = await _db.DeviceGroups.FindAsync([id], ct);
        if (group == null) return _404DeviceGroup(id);

        if (user.IsAdministrator() || userInformationService.ManagerOwnsGroup(user, group))
        {
            return group.ToViewItem();
        }

        return _403();
    }

    // POST: api/devicegroups
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(Reference))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    public async Task<ActionResult<Reference>> PostGroup(DeviceGroupCreateItem item, CancellationToken ct = default)
    {
        var user = await CurrentUser();
        if (user == null) return _403();

        var account = await _db.Accounts.FindAsync([item.AccountId], ct);
        if (account == null) return _404Account(item.AccountId);

        if (!(user.IsAdministrator() || userInformationService.ManagerOwnsAccount(user, account)))
        {
            return _403();
        }

        var (playlistIds, error) = await ValidateDeviceGroupPlaylists(item.Playlists, item.AccountId, ct);
        if (error != null) return error;

        var group = new DeviceGroup { Name = item.Name, AccountId = item.AccountId };
        var playlistLookup = item.Playlists.GroupBy(p => p.PlaylistId).ToDictionary(g => g.Key, g => g.First());
        foreach (var playlistId in playlistIds)
        {
            var playlist = playlistLookup[playlistId];
            group.PlaylistsDeviceGroup.Add(new PlaylistDeviceGroup
            {
                PlaylistId = playlist.PlaylistId,
                Play = playlist.Play
            });
        }
        _db.DeviceGroups.Add(group);
        await _db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(GetGroup), new { id = group.Id }, new Reference { Id = group.Id });
    }

    // PUT: api/devicegroups/{id}
    [HttpPut("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    public async Task<IActionResult> UpdateGroup(int id, DeviceGroupUpdateItem item, CancellationToken ct = default)
    {
        var user = await CurrentUser();
        if (user == null) return _403();

        var group = await _db.DeviceGroups
            .Include(g => g.PlaylistsDeviceGroup)
            .FirstOrDefaultAsync(g => g.Id == id, ct);
        if (group == null) return _404DeviceGroup(id);

        if (user.IsAdministrator() || userInformationService.ManagerOwnsGroup(user, group))
        {
            group.UpdateFrom(item);
            if (item.Playlists != null)
            {
                var (playlistIds, error) = await ValidateDeviceGroupPlaylists(item.Playlists, group.AccountId, ct);
                if (error != null) return error;

                var toRemove = group.PlaylistsDeviceGroup.ToList();
                group.PlaylistsDeviceGroup.Clear();
                _db.PlaylistDeviceGroups.RemoveRange(toRemove);

                var playlistLookup = item.Playlists.GroupBy(p => p.PlaylistId).ToDictionary(g => g.Key, g => g.First());
                foreach (var playlistId in playlistIds)
                {
                    var playlist = playlistLookup[playlistId];
                    group.PlaylistsDeviceGroup.Add(new PlaylistDeviceGroup
                    {
                        PlaylistId = playlist.PlaylistId,
                        Play = playlist.Play,
                        DeviceGroupId = group.Id
                    });
                }
            }
            await _db.SaveChangesAsync(ct);
            return NoContent();
        }

        return _403();
    }

    // DELETE: api/devicegroups/{id}
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    public async Task<IActionResult> DeleteGroup(int id, CancellationToken ct = default)
    {
        var user = await CurrentUser();
        if (user == null) return _403();

        var group = await _db.DeviceGroups.Include(g => g.Devices).FirstOrDefaultAsync(g => g.Id == id, ct);
        if (group == null) return _404DeviceGroup(id);

        if (!(user.IsAdministrator() || userInformationService.ManagerOwnsGroup(user, group))) return _403();

        foreach (var device in group.Devices)
        {
            device.DeviceGroupId = null;
        }

        _db.DeviceGroups.Remove(group);
        await _db.SaveChangesAsync(ct);

        return NoContent();
    }

    private async Task<(List<int> PlaylistIds, ObjectResult? Error)> ValidateDeviceGroupPlaylists(IEnumerable<PlaylistDeviceGroupItemDto> playlists, int accountId, CancellationToken ct)
    {
        var normalized = (playlists ?? Enumerable.Empty<PlaylistDeviceGroupItemDto>())
            .Select(p => p.PlaylistId)
            .Distinct()
            .ToList();
        if (normalized.Count == 0) return (normalized, null);

        var dbPlaylists = await _db.Playlists
            .AsNoTracking()
            .Where(p => normalized.Contains(p.Id))
            .Select(p => new { p.Id, p.AccountId })
            .ToListAsync(ct);

        var foundIds = dbPlaylists.Select(p => p.Id).ToHashSet();
        if (foundIds.Count != normalized.Count)
        {
            var missingId = normalized.Except(foundIds).First();
            return (normalized, _404Playlist(missingId));
        }

        var mismatch = dbPlaylists.FirstOrDefault(p => p.AccountId != accountId);
        if (mismatch != null)
        {
            return (normalized, _400VideoPlaylistAccountMismatch(mismatch.Id, accountId));
        }

        return (normalized, null);
    }
}

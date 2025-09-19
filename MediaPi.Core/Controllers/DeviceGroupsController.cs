// Copyright (c) 2025 sw.consulting
// This file is a part of Media Pi backend

using MediaPi.Core.Authorization;
using MediaPi.Core.Data;
using MediaPi.Core.Extensions;
using MediaPi.Core.Models;
using MediaPi.Core.RestModels;
using MediaPi.Core.Services;
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

        var group = new DeviceGroup { Name = item.Name, AccountId = item.AccountId };
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

        var group = await _db.DeviceGroups.FindAsync([id], ct);
        if (group == null) return _404DeviceGroup(id);

        if (user.IsAdministrator() || userInformationService.ManagerOwnsGroup(user, group))
        {
            group.UpdateFrom(item);
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
}

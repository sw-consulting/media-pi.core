// MIT License
//
// Copyright (c) 2025 Maxim [maxirmx] Samsonov (www.sw.consulting)
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using MediaPi.Core.Authorization;
using MediaPi.Core.Data;
using MediaPi.Core.Models;
using MediaPi.Core.RestModels;
using MediaPi.Core.Extensions;

namespace MediaPi.Core.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
[Produces("application/json")]
[ProducesResponseType(StatusCodes.Status401Unauthorized, Type = typeof(ErrMessage))]
public class DeviceGroupsController(
    IHttpContextAccessor httpContextAccessor,
    AppDbContext db,
    ILogger<DeviceGroupsController> logger) : MediaPiControllerBase(httpContextAccessor, db, logger)
{
    private async Task<User?> CurrentUser()
    {
        return await _db.Users
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .Include(u => u.UserAccounts)
            .FirstOrDefaultAsync(u => u.Id == _curUserId);
    }

    private static List<int> GetUserAccountIds(User user)
    {
        return [.. user.UserAccounts.Select(ua => ua.AccountId)];
    }

    private static bool ManagerOwnsGroup(User user, DeviceGroup group)
    {
        if (!user.IsManager()) return false;
        var accountIds = GetUserAccountIds(user);
        return accountIds.Contains(group.AccountId);
    }

    // GET: api/devicegroups
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IEnumerable<DeviceGroupViewItem>))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    public async Task<ActionResult<IEnumerable<DeviceGroupViewItem>>> GetAll()
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
            var accountIds = GetUserAccountIds(user);
            query = query.Where(g => accountIds.Contains(g.AccountId));
        }
        else
        {
            return _403();
        }

        var groups = await query.ToListAsync();
        return groups.Select(g => g.ToViewItem()).ToList();
    }

    // GET: api/devicegroups/{id}
    [HttpGet("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(DeviceGroupViewItem))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    public async Task<ActionResult<DeviceGroupViewItem>> GetGroup(int id)
    {
        var user = await CurrentUser();
        if (user == null) return _403();

        var group = await _db.DeviceGroups.FindAsync(id);
        if (group == null) return _404DeviceGroup(id);

        if (user.IsAdministrator() || ManagerOwnsGroup(user, group))
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
    public async Task<ActionResult<Reference>> PostGroup(DeviceGroupCreateItem item)
    {
        var user = await CurrentUser();
        if (user == null) return _403();

        int accountId;
        if (user.IsAdministrator())
        {
            if (!await _db.Accounts.AnyAsync(a => a.Id == item.AccountId)) return _404Account(item.AccountId);
            accountId = item.AccountId;
        }
        else if (user.IsManager())
        {
            var accountIds = GetUserAccountIds(user);
            if (accountIds.Count == 0) return _403();
            accountId = accountIds[0];
        }
        else
        {
            return _403();
        }

        var group = new DeviceGroup { Name = item.Name, AccountId = accountId };
        _db.DeviceGroups.Add(group);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetGroup), new { id = group.Id }, new Reference { Id = group.Id });
    }

    // PUT: api/devicegroups/{id}
    [HttpPut("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    public async Task<IActionResult> UpdateGroup(int id, DeviceGroupUpdateItem item)
    {
        var user = await CurrentUser();
        if (user == null) return _403();

        var group = await _db.DeviceGroups.FindAsync(id);
        if (group == null) return _404DeviceGroup(id);

        if (user.IsAdministrator())
        {
            if (item.AccountId.HasValue)
            {
                if (!await _db.Accounts.AnyAsync(a => a.Id == item.AccountId.Value)) return _404Account(item.AccountId.Value);
                group.AccountId = item.AccountId.Value;
            }
        }
        else if (ManagerOwnsGroup(user, group))
        {
            // Account operators cannot change accountId
        }
        else
        {
            return _403();
        }

        group.UpdateFrom(item);

        _db.Entry(group).State = EntityState.Modified;
        await _db.SaveChangesAsync();

        return NoContent();
    }

    // DELETE: api/devicegroups/{id}
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    public async Task<IActionResult> DeleteGroup(int id)
    {
        var user = await CurrentUser();
        if (user == null) return _403();

        var group = await _db.DeviceGroups.Include(g => g.Devices).FirstOrDefaultAsync(g => g.Id == id);
        if (group == null) return _404DeviceGroup(id);

        if (!(user.IsAdministrator() || ManagerOwnsGroup(user, group))) return _403();

        foreach (var device in group.Devices)
        {
            device.DeviceGroupId = null;
        }

        _db.DeviceGroups.Remove(group);
        await _db.SaveChangesAsync();

        return NoContent();
    }
}


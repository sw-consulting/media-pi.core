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

using System.Net;
using System.Linq;

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
public class DevicesController(
    IHttpContextAccessor httpContextAccessor,
    AppDbContext db,
    ILogger<DevicesController> logger) : MediaPiControllerBase(httpContextAccessor, db, logger)
{
    private async Task<User?> CurrentUser()
    {
        return await _db.Users
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .Include(u => u.UserAccounts)
            .FirstOrDefaultAsync(u => u.Id == _curUserId);
    }

    private static bool ManagerOwnsDevice(User user, Device device)
    {
        if (!user.IsManager()) return false;
        var accountIds = user.UserAccounts.Select(ua => ua.AccountId);
        return device.AccountId != null && accountIds.Contains(device.AccountId.Value);
    }

    // POST: api/devices/register
    [AllowAnonymous]
    [HttpPost("register")]
    [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(Reference))]
    public async Task<ActionResult<Reference>> Register()
    {
        var ipAddress = HttpContext.Connection.RemoteIpAddress;
        if (ipAddress?.IsIPv4MappedToIPv6 ?? false)
        {
            ipAddress = ipAddress.MapToIPv4();
        }
        var ip = ipAddress?.ToString() ?? "";

        if (string.IsNullOrWhiteSpace(ip)) return _400Ip(ip);

        if (await _db.Devices.AnyAsync(d => d.IpAddress == ip)) return _409Ip(ip);

        var device = new Device { Name = string.Empty, IpAddress = ip };
        _db.Devices.Add(device);
        await _db.SaveChangesAsync();
        device.Name = $"Устройство №{device.Id}";
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetDevice), new { id = device.Id }, new Reference { Id = device.Id });
    }

    // GET: api/devices
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IEnumerable<DeviceViewItem>))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    public async Task<ActionResult<IEnumerable<DeviceViewItem>>> GetAll()
    {
        var user = await CurrentUser();
        if (user == null) return _403();

        IQueryable<Device> query = _db.Devices;
        if (user.IsAdministrator())
        {
            // all devices
        }
        else if (user.IsManager())
        {
            var accountIds = user.UserAccounts.Select(ua => ua.AccountId).ToList();
            query = query.Where(d => d.AccountId != null && accountIds.Contains(d.AccountId.Value));
        }
        else if (user.HasRole(UserRoleConstants.InstallationEngineer))
        {
            query = query.Where(d => d.AccountId == null);
        }
        else
        {
            return _403();
        }

        var devices = await query.ToListAsync();
        return devices.Select(d => d.ToViewItem()).ToList();
    }

    // GET: api/devices/by-account/{accountId?}
    [HttpGet("by-account/{accountId?}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IEnumerable<DeviceViewItem>))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    public async Task<ActionResult<IEnumerable<DeviceViewItem>>> GetAllByAccount(int? accountId)
    {
        var user = await CurrentUser();
        if (user == null) return _403();

        IQueryable<Device> query = _db.Devices;
        if (user.IsAdministrator())
        {
            if (accountId == null)
                query = query.Where(d => d.AccountId == null);
            else
                query = query.Where(d => d.AccountId == accountId);
        }
        else if (user.IsManager())
        {
            if (accountId == null) return _403();
            var accountIds = user.UserAccounts.Select(ua => ua.AccountId).ToList();
            if (!accountIds.Contains(accountId.Value)) return _403();
            query = query.Where(d => d.AccountId == accountId.Value);
        }
        else if (user.HasRole(UserRoleConstants.InstallationEngineer))
        {
            if (accountId != null) return _403();
            query = query.Where(d => d.AccountId == null);
        }
        else
        {
            return _403();
        }

        var devices = await query.ToListAsync();
        return devices.Select(d => d.ToViewItem()).ToList();
    }

    // GET: api/devices/by-device-group/{deviceGroupId?}
    [HttpGet("by-device-group/{deviceGroupId?}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IEnumerable<DeviceViewItem>))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    public async Task<ActionResult<IEnumerable<DeviceViewItem>>> GetAllByDeviceGroup(int? deviceGroupId)
    {
        var user = await CurrentUser();
        if (user == null) return _403();

        IQueryable<Device> query = _db.Devices;
        if (user.IsAdministrator())
        {
            if (deviceGroupId == null)
                query = query.Where(d => d.DeviceGroupId == null);
            else
                query = query.Where(d => d.DeviceGroupId == deviceGroupId);
        }
        else if (user.IsManager())
        {
            var accountIds = user.UserAccounts.Select(ua => ua.AccountId).ToList();
            query = query.Where(d => d.AccountId != null && accountIds.Contains(d.AccountId.Value));
            if (deviceGroupId == null)
            {
                query = query.Where(d => d.DeviceGroupId == null);
            }
            else
            {
                var allowedGroupIds = await _db.DeviceGroups
                    .Where(dg => accountIds.Contains(dg.AccountId))
                    .Select(dg => dg.Id)
                    .ToListAsync();
                if (!allowedGroupIds.Contains(deviceGroupId.Value)) return _403();
                query = query.Where(d => d.DeviceGroupId == deviceGroupId.Value);
            }
        }
        else if (user.HasRole(UserRoleConstants.InstallationEngineer))
        {
            if (deviceGroupId != null) return _403();
            query = query.Where(d => d.AccountId == null && d.DeviceGroupId == null);
        }
        else
        {
            return _403();
        }

        var devices = await query.ToListAsync();
        return devices.Select(d => d.ToViewItem()).ToList();
    }

    // GET: api/devices/5
    [HttpGet("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(DeviceViewItem))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    public async Task<ActionResult<DeviceViewItem>> GetDevice(int id)
    {
        var user = await CurrentUser();
        if (user == null) return _403();

        var device = await _db.Devices.FindAsync(id);
        if (device == null) return _404Device(id);

        if (user.IsAdministrator() || ManagerOwnsDevice(user, device) ||
            (user.HasRole(UserRoleConstants.InstallationEngineer) && device.AccountId == null))
        {
            return device.ToViewItem();
        }

        return _403();
    }

    // PUT: api/devices/5
    [HttpPut("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    public async Task<IActionResult> UpdateDevice(int id, DeviceUpdateItem item)
    {
        var user = await CurrentUser();
        if (user == null || !user.IsAdministrator()) return _403();

        var device = await _db.Devices.FindAsync(id);
        if (device == null) return _404Device(id);

        if (item.IpAddress != null)
        {
            if (!IPAddress.TryParse(item.IpAddress, out var addr)) return _400Ip(item.IpAddress);
            if (addr.IsIPv4MappedToIPv6) addr = addr.MapToIPv4();
            var ip = addr.ToString();
            if (await _db.Devices.AnyAsync(d => d.IpAddress == ip && d.Id != id)) return _409Ip(ip);
            device.IpAddress = ip;
        }

        device.UpdateFrom(item);
        _db.Entry(device).State = EntityState.Modified;
        await _db.SaveChangesAsync();

        return NoContent();
    }

    // DELETE: api/devices/5
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    public async Task<IActionResult> DeleteDevice(int id)
    {
        var user = await CurrentUser();
        if (user == null || !user.IsAdministrator()) return _403();

        var device = await _db.Devices.FindAsync(id);
        if (device == null) return _404Device(id);

        _db.Devices.Remove(device);
        await _db.SaveChangesAsync();

        return NoContent();
    }

    // PATCH: api/devices/{id}/assign-group
    [HttpPatch("{id}/assign-group")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    public async Task<IActionResult> AssignGroup(int id, DeviceAssignGroupItem item)
    {
        var user = await CurrentUser();
        if (user == null) return _403();

        var device = await _db.Devices.FindAsync(id);
        if (device == null) return _404Device(id);

        if (user.IsAdministrator() || ManagerOwnsDevice(user, device))
        {
            device.AssignGroupFrom(item);
            _db.Entry(device).State = EntityState.Modified;
            await _db.SaveChangesAsync();
            return NoContent();
        }

        return _403();
    }

    // PATCH: api/devices/{id}/initial-assign-account
    [HttpPatch("{id}/initial-assign-account")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    public async Task<IActionResult> InitialAssignAccount(int id, DeviceInitialAssignAccountItem item)
    {
        var user = await CurrentUser();
        if (user == null) return _403();

        var device = await _db.Devices.FindAsync(id);
        if (device == null) return _404Device(id);

        if (user.IsAdministrator() ||
            (user.HasRole(UserRoleConstants.InstallationEngineer) && device.AccountId == null))
        {
            device.InitialAssignAccountFrom(item);
            _db.Entry(device).State = EntityState.Modified;
            await _db.SaveChangesAsync();
            return NoContent();
        }

        return _403();
    }
}


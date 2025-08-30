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
//
// This file is a part of Media Pi backend application

using MediaPi.Core.Authorization;
using MediaPi.Core.Data;
using MediaPi.Core.Extensions;
using MediaPi.Core.Models;
using MediaPi.Core.RestModels;
using MediaPi.Core.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Net;

namespace MediaPi.Core.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
[Produces("application/json")]
[ProducesResponseType(StatusCodes.Status401Unauthorized, Type = typeof(ErrMessage))]
public class DevicesController(
    IHttpContextAccessor httpContextAccessor,
    IUserInformationService userInformationService,
    AppDbContext db,
    ILogger<DevicesController> logger,
    DeviceEventsService deviceEventsService,
    IDeviceMonitoringService monitoringService) : MediaPiControllerBase(httpContextAccessor, db, logger)
{
    // POST: api/devices/register
    [AllowAnonymous]
    [HttpPost("register")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(DeviceRegisterResponse))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status409Conflict, Type = typeof(ErrMessage))]
    public async Task<ActionResult<DeviceRegisterResponse>> Register([FromBody] DeviceRegisterRequest req, CancellationToken ct)
    {
        var ipAddress = HttpContext.Connection.RemoteIpAddress;
        if (ipAddress?.IsIPv4MappedToIPv6 ?? false)
        {
            ipAddress = ipAddress.MapToIPv4();
        }
        var ip = ipAddress?.ToString() ?? "";

        if (string.IsNullOrWhiteSpace(ip)) return _400Ip(ip);

        if (await _db.Devices.AnyAsync(d => d.IpAddress == ip, ct)) return _409Ip(ip);

        var now = DateTime.UtcNow;
        var device = new Device 
        { 
            Name = string.Empty, 
            IpAddress = ip,
            PublicKeyOpenSsh = req.PublicKeyOpenSsh ?? string.Empty,
            SshUser = string.IsNullOrWhiteSpace(req.SshUser) ? "pi" : req.SshUser!
        };
        
        _db.Devices.Add(device);
        await _db.SaveChangesAsync(ct);
        
        device.Name = $"Устройство №{device.Id}";
        await _db.SaveChangesAsync(ct);

        deviceEventsService.OnDeviceCreated(device);

        return Ok(new DeviceRegisterResponse { Id = device.Id });
    }

    // GET: api/devices
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IEnumerable<DeviceViewItem>))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    public async Task<ActionResult<IEnumerable<DeviceViewItem>>> GetAll(CancellationToken ct = default)
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
            var accountIds = userInformationService.GetUserAccountIds(user);
            query = query.Where(d => d.AccountId != null && accountIds.Contains(d.AccountId.Value));
        }
        else if (user.IsEngineer())
        {
            query = query.Where(d => d.AccountId == null);
        }
        else
        {
            return _403();
        }

        var devices = await query.ToListAsync(ct);
        return devices.Select(d =>
        {
            monitoringService.TryGetStatusItem(d.Id, out var status);
            return d.ToViewItem(status);
        }).ToList();
    }

    // GET: api/devices/by-account/{accountId?}
    [HttpGet("by-account/{accountId?}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IEnumerable<DeviceViewItem>))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    public async Task<ActionResult<IEnumerable<DeviceViewItem>>> GetAllByAccount(int? accountId, CancellationToken ct = default)
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
            var accountIds = userInformationService.GetUserAccountIds(user);
            if (!accountIds.Contains(accountId.Value)) return _403();
            query = query.Where(d => d.AccountId == accountId.Value);
        }
        else if (user.IsEngineer())
        {
            if (accountId != null) return _403();
            query = query.Where(d => d.AccountId == null);
        }
        else
        {
            return _403();
        }

        var devices = await query.ToListAsync(ct);
        return devices.Select(d =>
        {
            monitoringService.TryGetStatusItem(d.Id, out var status);
            return d.ToViewItem(status);
        }).ToList();
    }

    // GET: api/devices/by-device-group/{deviceGroupId?}
    [HttpGet("by-device-group/{deviceGroupId?}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IEnumerable<DeviceViewItem>))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    public async Task<ActionResult<IEnumerable<DeviceViewItem>>> GetAllByDeviceGroup(int? deviceGroupId, CancellationToken ct = default)
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
            var accountIds = userInformationService.GetUserAccountIds(user);
            if (deviceGroupId != null)
            {
                // Check if the group belongs to the manager's accounts
                bool ownsGroup = await _db.DeviceGroups.AnyAsync(dg => dg.Id == deviceGroupId.Value && accountIds.Contains(dg.AccountId), ct);
                if (!ownsGroup)
                    return _403();
            }
            query = query.Where(d => d.AccountId != null && accountIds.Contains(d.AccountId.Value));
            if (deviceGroupId == null)
            {
                query = query.Where(d => d.DeviceGroupId == null);
            }
            else
            {
                query = query.Where(d => d.DeviceGroupId == deviceGroupId.Value);
            }
        }
        else if (user.IsEngineer())
        {
            if (deviceGroupId != null) return _403();
            query = query.Where(d => d.AccountId == null && d.DeviceGroupId == null);
        }
        else
        {
            return _403();
        }

        var devices = await query.ToListAsync(ct);
        return devices.Select(d =>
        {
            monitoringService.TryGetStatusItem(d.Id, out var status);
            return d.ToViewItem(status);
        }).ToList();
    }

    // GET: api/devices/5
    [HttpGet("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(DeviceViewItem))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    public async Task<ActionResult<DeviceViewItem>> GetDevice(int id, CancellationToken ct = default)
    {
        var user = await CurrentUser();
        if (user == null) return _403();

        var device = await _db.Devices.FindAsync([id], ct);
        if (device == null) return _404Device(id);

        if (user.IsAdministrator() || userInformationService.ManagerOwnsDevice(user, device) ||
            (user.IsEngineer() && device.AccountId == null))
        {
            monitoringService.TryGetStatusItem(device.Id, out var status);
            return device.ToViewItem(status);
        }

        return _403();
    }

    // PUT: api/devices/5
    [HttpPut("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    public async Task<IActionResult> UpdateDevice(int id, DeviceUpdateItem item, CancellationToken ct = default)
    {
        var user = await CurrentUser();
        if (user == null || !user.IsAdministrator()) return _403();

        var device = await _db.Devices.FindAsync([id], ct);
        if (device == null) return _404Device(id);

        if (item.IpAddress != null)
        {
            if (!IPAddress.TryParse(item.IpAddress, out var addr)) return _400Ip(item.IpAddress);
            if (addr.IsIPv4MappedToIPv6) addr = addr.MapToIPv4();
            var ip = addr.ToString();
            if (await _db.Devices.AnyAsync(d => d.IpAddress == ip && d.Id != id, ct)) return _409Ip(ip);
            device.IpAddress = ip;
        }

        device.UpdateFrom(item);
        await _db.SaveChangesAsync(ct);

        deviceEventsService.OnDeviceUpdated(device);

        return NoContent();
    }

    // DELETE: api/devices/5
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    public async Task<IActionResult> DeleteDevice(int id, CancellationToken ct = default)
    {
        var user = await CurrentUser();
        if (user == null || !user.IsAdministrator()) return _403();

        var device = await _db.Devices.FindAsync([id], ct);
        if (device == null) return _404Device(id);

        _db.Devices.Remove(device);
        await _db.SaveChangesAsync(ct);

        deviceEventsService.OnDeviceDeleted(id);

        return NoContent();
    }

    // PATCH: api/devices/assign-group/{id}
    [HttpPatch("assign-group/{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status409Conflict, Type = typeof(ErrMessage))]
    public async Task<IActionResult> AssignGroup(int id, Reference item, CancellationToken ct = default)
    {
        var user = await CurrentUser();
        if (user == null) return _403();

        var device = await _db.Devices.FindAsync([id], ct);
        if (device == null) return _404Device(id);

        if (user.IsAdministrator() || userInformationService.ManagerOwnsDevice(user, device))
        {
            // Validate device group assignment if not setting to null
            if (item.Id != 0)
            {
                var deviceGroup = await _db.DeviceGroups.FindAsync([item.Id], ct);
                if (deviceGroup == null) return _404DeviceGroup(item.Id);

                // Check if device group belongs to the same account as the device
                if (device.AccountId != deviceGroup.AccountId)
                {
                    return _409DeviceGroupAccountMismatch(item.Id, device.AccountId);
                }
            }

            device.AssignGroupFrom(item);
            await _db.SaveChangesAsync(ct);

            deviceEventsService.OnDeviceUpdated(device);

            return NoContent();
        }

        return _403();
    }

    // PATCH: api/devices/assign-account/{id}
    [HttpPatch("assign-account/{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    public async Task<IActionResult> AssignAccount(int id, Reference item, CancellationToken ct = default)
    {
        var user = await CurrentUser();
        if (user == null) return _403();

        var device = await _db.Devices.FindAsync([id], ct);
        if (device == null) return _404Device(id);

        if (user.IsAdministrator() ||
            (user.IsEngineer() && device.AccountId == null))
        {
            device.AssignAccountFrom(item);
            await _db.SaveChangesAsync(ct);

            deviceEventsService.OnDeviceUpdated(device);

            return NoContent();
        }

        return _403();
    }
}


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
using MediaPi.Core.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
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
    IDeviceMonitoringService monitoringService,
    ISshClientKeyProvider sshClientKeyProvider,
    IMediaPiAgentClient mediaPiAgentClient) : MediaPiControllerBase(httpContextAccessor, db, logger)
{
    // POST: api/devices/register
    [AllowAnonymous]
    [HttpPost("register")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(DeviceRegisterResponse))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status409Conflict, Type = typeof(ErrMessage))]
    public async Task<ActionResult<DeviceRegisterResponse>> Register([FromBody] DeviceRegisterRequest req, CancellationToken ct)
    {
        string ip;
        if (!string.IsNullOrWhiteSpace(req.IpAddress))
        {
            if (!IPAddress.TryParse(req.IpAddress, out var addr)) return _400Ip(req.IpAddress);
            if (addr.IsIPv4MappedToIPv6) addr = addr.MapToIPv4();
            ip = addr.ToString();
        }
        else
        {
            var ipAddress = HttpContext.Connection.RemoteIpAddress;
            if (ipAddress?.IsIPv4MappedToIPv6 ?? false)
            {
                ipAddress = ipAddress.MapToIPv4();
            }
            ip = ipAddress?.ToString() ?? string.Empty;
        }

        if (string.IsNullOrWhiteSpace(ip)) return _400Ip(ip);

        if (await _db.Devices.AnyAsync(d => d.IpAddress == ip, ct)) return _409Ip(ip);

        // Create device with auto-generated ID
        var device = new Device
        {
            Name = string.IsNullOrWhiteSpace(req.Name) ? "Устройство" : req.Name!, // Placeholder name if not provided
            IpAddress = ip,
            PublicKeyOpenSsh = req.PublicKeyOpenSsh ?? string.Empty,
            SshUser = string.IsNullOrWhiteSpace(req.SshUser) ? "pi" : req.SshUser!
        };

        _db.Devices.Add(device);
        await _db.SaveChangesAsync(ct);

        // Update name with ID if no custom name was provided
        if (string.IsNullOrWhiteSpace(req.Name))
        {
            device.Name = $"Устройство №{device.Id}";
            await _db.SaveChangesAsync(ct);
        }

        deviceEventsService.OnDeviceCreated(device);

        return Ok(new DeviceRegisterResponse
        {
            Id = device.Id,
            ServerPublicSshKey = sshClientKeyProvider.GetPublicKey()
        });
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

        if (userInformationService.UserCanViewDevice(user, device))
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

    // GET: api/devices/{id}/services
    [HttpGet("{id}/services")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(MediaPiAgentListResponse))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status502BadGateway, Type = typeof(ErrMessage))]
    public async Task<ActionResult<MediaPiAgentListResponse>> ListServices(int id, CancellationToken ct = default)
    {
        return await ExecuteAgentOperation(
            id,
            "list services",
            (device, token) => mediaPiAgentClient.ListUnitsAsync(device, token),
            ct);
    }

    // POST: api/devices/{id}/services/{unit}/start
    [HttpPost("{id}/services/{unit}/start")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(MediaPiAgentUnitResultResponse))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status502BadGateway, Type = typeof(ErrMessage))]
    public async Task<ActionResult<MediaPiAgentUnitResultResponse>> StartService(int id, string unit, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(unit)) return _400ServiceUnit(unit);
        var service = unit.Trim();
        return await ExecuteAgentOperation(
            id,
            "start service",
            (device, token) => mediaPiAgentClient.StartUnitAsync(device, service, token),
            ct,
            service);
    }

    // POST: api/devices/{id}/services/{unit}/stop
    [HttpPost("{id}/services/{unit}/stop")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(MediaPiAgentUnitResultResponse))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status502BadGateway, Type = typeof(ErrMessage))]
    public async Task<ActionResult<MediaPiAgentUnitResultResponse>> StopService(int id, string unit, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(unit)) return _400ServiceUnit(unit);
        var service = unit.Trim();
        return await ExecuteAgentOperation(
            id,
            "stop service",
            (device, token) => mediaPiAgentClient.StopUnitAsync(device, service, token),
            ct,
            service);
    }

    // POST: api/devices/{id}/services/{unit}/restart
    [HttpPost("{id}/services/{unit}/restart")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(MediaPiAgentUnitResultResponse))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status502BadGateway, Type = typeof(ErrMessage))]
    public async Task<ActionResult<MediaPiAgentUnitResultResponse>> RestartService(int id, string unit, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(unit)) return _400ServiceUnit(unit);
        var service = unit.Trim();
        return await ExecuteAgentOperation(
            id,
            "restart service",
            (device, token) => mediaPiAgentClient.RestartUnitAsync(device, service, token),
            ct,
            service);
    }

    // POST: api/devices/{id}/services/{unit}/enable
    [HttpPost("{id}/services/{unit}/enable")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(MediaPiAgentEnableResponse))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status502BadGateway, Type = typeof(ErrMessage))]
    public async Task<ActionResult<MediaPiAgentEnableResponse>> EnableService(int id, string unit, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(unit)) return _400ServiceUnit(unit);
        var service = unit.Trim();
        return await ExecuteAgentOperation(
            id,
            "enable service",
            (device, token) => mediaPiAgentClient.EnableUnitAsync(device, service, token),
            ct,
            service);
    }

    // POST: api/devices/{id}/services/{unit}/disable
    [HttpPost("{id}/services/{unit}/disable")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(MediaPiAgentEnableResponse))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status502BadGateway, Type = typeof(ErrMessage))]
    public async Task<ActionResult<MediaPiAgentEnableResponse>> DisableService(int id, string unit, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(unit)) return _400ServiceUnit(unit);
        var service = unit.Trim();
        return await ExecuteAgentOperation(
            id,
            "disable service",
            (device, token) => mediaPiAgentClient.DisableUnitAsync(device, service, token),
            ct,
            service);
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

        if (userInformationService.UserCanAssignGroup(user, device))
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

    private async Task<(Device? Device, ActionResult? Error)> GetDeviceForServiceAsync(int id, CancellationToken ct)
    {
        var user = await CurrentUser();
        if (user == null) return (null, _403());

        var device = await _db.Devices.FindAsync([id], ct);
        if (device == null) return (null, _404Device(id));

        if (!userInformationService.UserCanManageDeviceServices(user, device)) return (null, _403());

        return (device, null);
    }
    
    private async Task<ActionResult<TResponse>> ExecuteAgentOperation<TResponse>(
        int id,
        string operationName,
        Func<Device, CancellationToken, Task<TResponse>> operation,
        CancellationToken ct,
        string? unit = null)
        where TResponse : MediaPiAgentResponse
    {
        var (device, error) = await GetDeviceForServiceAsync(id, ct);
        if (error != null) return error;

        var targetDevice = device!;

        try
        {
            var response = await operation(targetDevice, ct);
            if (!response.Ok)
            {
                if (string.IsNullOrWhiteSpace(unit))
                {
                    logger.LogWarning("Агент не выполнил операцию {Operation} для устройства {DeviceId}: {Error}", operationName, id, response.Error ?? "неизвестная ошибка");
                }
                else
                {
                    logger.LogWarning("Агент не выполнил операцию {Operation} для устройства {DeviceId}, сервиса {Unit}: {Error}", operationName, id, unit, response.Error ?? "неизвестная ошибка");
                }
                return _502Agent(response.Error);
            }

            return Ok(response);
        }
        catch (Exception ex)
        {
            if (string.IsNullOrWhiteSpace(unit))
            {
                logger.LogError(ex, "Ошибка при операции {Operation} для устройства {DeviceId}", operationName, id);
            }
            else
            {
                logger.LogError(ex, "Ошибка при операции {Operation} для устройства {DeviceId}, сервиса {Unit}", operationName, id, unit);
            }

            return _502Agent();
        }
    }
}


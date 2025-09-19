// Copyright (c) 2025 sw.consulting
// This file is a part of Media Pi backend

using MediaPi.Core.Authorization;
using MediaPi.Core.RestModels;
using MediaPi.Core.Services;
using MediaPi.Core.Settings;
using Microsoft.AspNetCore.Mvc;

namespace MediaPi.Core.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
[Produces("application/json")]
[ProducesResponseType(StatusCodes.Status401Unauthorized, Type = typeof(ErrMessage))]
public class DeviceStatusesController(IDeviceMonitoringService monitoringService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IEnumerable<DeviceStatusItem>))]
    public ActionResult<IEnumerable<DeviceStatusItem>> GetAll()
    {
        var result = monitoringService.Snapshot
            .Select(kvp => new DeviceStatusItem(kvp.Key, kvp.Value))
            .ToList();
        return Ok(result);
    }

    [HttpGet("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(DeviceStatusItem))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    public async Task<ActionResult<DeviceStatusItem>> Get(int id, CancellationToken ct = default)
    {
        var snapshot = await monitoringService.Test(id, ct);
        if (snapshot is null)
        {
            return NotFound(new ErrMessage { Msg = $"Не удалось найти устройство [id={id}]" });
        }
        return Ok(new DeviceStatusItem(id, snapshot));
    }

    [HttpPost("{id}/test")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(DeviceStatusItem))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    public async Task<ActionResult<DeviceStatusItem>> Test(int id, CancellationToken ct = default)
    {
        var snapshot = await monitoringService.Test(id, ct);
        if (snapshot is null)
        {
            return NotFound(new ErrMessage { Msg = $"Не удалось найти устройство [id={id}]" });
        }
        return Ok(new DeviceStatusItem(id, snapshot));
    }

    [HttpGet("stream")]
    [Produces("text/event-stream")]
    public async Task Stream(CancellationToken cancellationToken)
    {
        Response.Headers.Append("Cache-Control", "no-cache");
        Response.Headers.Append("Connection", "keep-alive");
        Response.ContentType = "text/event-stream";

        try
        {
            await foreach (var update in monitoringService.Subscribe(cancellationToken))
            {
                var item = new DeviceStatusItem(update.DeviceId, update.Snapshot);
                var data = $"data: {System.Text.Json.JsonSerializer.Serialize(item, JOptions.StreamJsonOptions)}\n\n";
                await Response.WriteAsync(data, cancellationToken);
                await Response.Body.FlushAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when the connection is closed
        }
    }
}

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
using MediaPi.Core.RestModels;
using MediaPi.Core.Services;
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
    public ActionResult<DeviceStatusItem> Get(int id)
    {
        if (monitoringService.TryGetStatus(id, out var snapshot))
        {
            return Ok(new DeviceStatusItem(id, snapshot));
        }
        return NotFound(new ErrMessage { Msg = $"Не удалось найти устройство [id={id}]" });
    }

    [HttpPost("{id}/test")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(DeviceStatusItem))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    public async Task<ActionResult<DeviceStatusItem>> Test(int id)
    {
        var snapshot = await monitoringService.Test(id);
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
        // Keep the existing authorization - it will work with fetch() headers
        Response.Headers.Append("Cache-Control", "no-cache");
        Response.Headers.Append("Connection", "keep-alive");
        Response.ContentType = "text/event-stream";

        try
        {
            await foreach (var update in monitoringService.Subscribe(cancellationToken))
            {
                var item = new DeviceStatusItem(update.DeviceId, update.Snapshot);
                var data = $"data: {System.Text.Json.JsonSerializer.Serialize(item)}\n\n";
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

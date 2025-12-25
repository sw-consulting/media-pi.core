// Copyright (c) 2025 sw.consulting
// This file is a part of Media Pi backend

using MediaPi.Core.Data;
using MediaPi.Core.RestModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace MediaPi.Core.Authorization;

public class AuthorizeDeviceByIpMiddleware
{
    private readonly RequestDelegate _next;

    public AuthorizeDeviceByIpMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task Invoke(HttpContext context, AppDbContext db, ILogger<AuthorizeDeviceByIpMiddleware> logger)
    {
        var endpoint = context.GetEndpoint();
        var authorizeDevice = endpoint?.Metadata.GetMetadata<AuthorizeDeviceAttribute>();
        if (authorizeDevice == null)
        {
            await _next(context);
            return;
        }

        var allowAnonymous = endpoint?.Metadata.GetMetadata<AllowAnonymousAttribute>() != null;
        if (allowAnonymous)
        {
            await _next(context);
            return;
        }

        var ipAddress = context.Connection.RemoteIpAddress?.MapToIPv4().ToString();
        if (string.IsNullOrWhiteSpace(ipAddress))
        {
            await RejectAsync(context, logger, "IP адрес устройства не определен.");
            return;
        }

        var device = await db.Devices.AsNoTracking().FirstOrDefaultAsync(d => d.IpAddress == ipAddress);
        if (device == null)
        {
            await RejectAsync(context, logger, "Устройство не зарегистрировано.");
            return;
        }

        context.Items["DeviceId"] = device.Id;
        await _next(context);
    }

    private static async Task RejectAsync(HttpContext context, ILogger<AuthorizeDeviceByIpMiddleware> logger, string message)
    {
        logger.LogWarning(message);
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        await context.Response.WriteAsJsonAsync(new ErrMessage { Msg = message });
    }
}

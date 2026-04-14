// Copyright (C) 2025-2026 sw.consulting
// This file is a part of Media Pi backend

using MediaPi.Core.Data;
using MediaPi.Core.RestModels;
using Microsoft.EntityFrameworkCore;

namespace MediaPi.Core.Authorization;

/// <summary>
/// Middleware to authorize devices by server key via X-Device-Id header.
/// Devices must provide their server_key in the X-Device-Id header.
/// </summary>
public class AuthorizeDeviceByXIdMiddleware
{
    private readonly RequestDelegate _next;
    private const string DeviceIdHeaderName = "X-Device-Id";

    public AuthorizeDeviceByXIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task Invoke(HttpContext context, AppDbContext db, ILogger<AuthorizeDeviceByXIdMiddleware> logger)
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

        // Extract server_key from X-Device-Id header
        if (!context.Request.Headers.TryGetValue(DeviceIdHeaderName, out var serverKeyValue))
        {
            await RejectAsync(context, logger, $"Заголовок {DeviceIdHeaderName} не найден в запросе.");
            return;
        }

        var serverKey = serverKeyValue.ToString().Trim();
        if (string.IsNullOrWhiteSpace(serverKey))
        {
            await RejectAsync(context, logger, $"Заголовок {DeviceIdHeaderName} пуст или содержит только пробелы.");
            return;
        }

        // Lookup device by server_key
        var device = await db.Devices.AsNoTracking()
            .FirstOrDefaultAsync(d => d.ServerKey == serverKey);
        if (device == null)
        {
            await RejectAsync(context, logger, "Устройство не найдено или не зарегистрировано.");
            return;
        }

        context.Items["DeviceId"] = device.Id;
        await _next(context);
    }

    private static async Task RejectAsync(HttpContext context, ILogger<AuthorizeDeviceByXIdMiddleware> logger, string message)
    {
        logger.LogWarning(message);
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        await context.Response.WriteAsJsonAsync(new ErrMessage { Msg = message });
    }
}


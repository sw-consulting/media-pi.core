// Copyright (c) 2025 sw.consulting
// This file is a part of Media Pi backend

using MediaPi.Core.RestModels;
using Microsoft.EntityFrameworkCore;

namespace MediaPi.Core.Middleware;

/// <summary>
/// Middleware to handle database constraint violations gracefully.
/// Catches DbUpdateException and returns appropriate HTTP responses.
/// </summary>
public class DatabaseConstraintMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<DatabaseConstraintMiddleware> _logger;

    public DatabaseConstraintMiddleware(RequestDelegate next, ILogger<DatabaseConstraintMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Database constraint violation occurred");

            context.Response.StatusCode = StatusCodes.Status409Conflict;
            context.Response.ContentType = "application/json";

            var error = GetErrorMessage(ex);
            await context.Response.WriteAsJsonAsync(error);
        }
    }

    private static ErrMessage GetErrorMessage(DbUpdateException ex)
    {
        var innerMessage = ex.InnerException?.Message ?? "";

        // Check for specific constraint violations
        if (innerMessage.Contains("playlist_device_group_play", StringComparison.OrdinalIgnoreCase))
        {
            return new ErrMessage { Msg = "Группа устройств может иметь не более одного проигрываемого плейлиста" };
        }

        if (innerMessage.Contains("unique", StringComparison.OrdinalIgnoreCase))
        {
            return new ErrMessage { Msg = "Нарушено уникальное ограничение базы данных" };
        }

        if (innerMessage.Contains("foreign key", StringComparison.OrdinalIgnoreCase))
        {
            return new ErrMessage { Msg = "Нарушено ограничение внешнего ключа базы данных" };
        }

        return new ErrMessage { Msg = "Нарушено ограничение целостности базы данных" };
    }
}

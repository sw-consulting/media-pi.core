// Copyright (c) 2025 sw.consulting
// This file is a part of Media Pi backend

using MediaPi.Core.RestModels;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace MediaPi.Core.Middleware;

/// <summary>
/// Middleware to handle database constraint violations gracefully.
/// Catches DbUpdateException and returns appropriate HTTP responses.
/// Inspects provider-specific exception details (Npgsql) for reliable constraint identification.
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

            // If response has already started (e.g., streaming, partial writes),
            // we cannot modify headers or write new content. Rethrow to let it propagate.
            if (context.Response.HasStarted)
            {
                _logger.LogError("Cannot write error response; response stream already started. Original exception will propagate.");
                throw;
            }

            context.Response.StatusCode = StatusCodes.Status409Conflict;
            context.Response.ContentType = "application/json";

            var error = GetErrorMessage(ex);
            await context.Response.WriteAsJsonAsync(error);
        }
    }

    private static ErrMessage GetErrorMessage(DbUpdateException ex)
    {
        // Try to extract provider-specific constraint details (PostgreSQL via Npgsql)
        var postgresException = FindPostgresException(ex);
        if (postgresException != null)
        {
            return GetPostgresErrorMessage(postgresException);
        }

        // Fallback: inspect InnerException message (provider-agnostic, less reliable)
        var innerMessage = ex.InnerException?.Message ?? "";
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

    /// <summary>
    /// Extracts PostgresException from the exception hierarchy (DbUpdateException -> InnerException chain).
    /// </summary>
    private static PostgresException? FindPostgresException(Exception? ex)
    {
        while (ex != null)
        {
            if (ex is PostgresException postgresEx)
                return postgresEx;
            ex = ex.InnerException;
        }
        return null;
    }

    /// <summary>
    /// Maps PostgreSQL constraint violations to user-friendly error messages.
    /// Uses SqlState (standard PostgreSQL error codes) and ConstraintName for reliable detection.
    /// </summary>
    private static ErrMessage GetPostgresErrorMessage(PostgresException ex)
    {
        // PostgreSQL unique constraint violation: SqlState = 23505
        if (ex.SqlState == "23505")
        {
            if (ex.ConstraintName?.Contains("ix_playlist_device_group_device_group_id", StringComparison.OrdinalIgnoreCase) ?? false)
            {
                return new ErrMessage { Msg = "Группа устройств может иметь не более одного проигрываемого плейлиста" };
            }

            return new ErrMessage { Msg = "Нарушено уникальное ограничение базы данных" };
        }

        // PostgreSQL foreign key constraint violation: SqlState = 23503
        if (ex.SqlState == "23503")
        {
            return new ErrMessage { Msg = "Нарушено ограничение внешнего ключа базы данных" };
        }

        // PostgreSQL check constraint violation: SqlState = 23514
        if (ex.SqlState == "23514")
        {
            return new ErrMessage { Msg = "Данные не соответствуют ограничениям на значения полей" };
        }

        // Other PostgreSQL integrity constraint violations: SqlState = 23001
        if (ex.SqlState == "23001")
        {
            return new ErrMessage { Msg = "Нарушено ограничение целостности базы данных" };
        }

        return new ErrMessage { Msg = "Нарушено ограничение целостности базы данных" };
    }
}

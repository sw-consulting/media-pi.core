// Copyright (C) 2025-2026 sw.consulting
// This file is a part of Media Pi backend

using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using MediaPi.Core.Middleware;
using MediaPi.Core.RestModels;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Npgsql;
using NUnit.Framework;

namespace MediaPi.Core.Tests.Middleware;

[TestFixture]
public class DatabaseConstraintMiddlewareTests
{
    private static DatabaseConstraintMiddleware CreateMiddleware(RequestDelegate next)
    {
        return new DatabaseConstraintMiddleware(next, Mock.Of<ILogger<DatabaseConstraintMiddleware>>());
    }

    private static DefaultHttpContext CreateContext()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static async Task<ErrMessage?> ReadResponseBody(HttpContext context)
    {
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        return await JsonSerializer.DeserializeAsync<ErrMessage>(
            context.Response.Body,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    }

    private static DbUpdateException MakeFallbackException(string innerMessage)
    {
        var inner = new Exception(innerMessage);
        return new DbUpdateException("DB error", inner);
    }

    private static DbUpdateException MakePostgresException(string constraintName, string sqlState = "23505")
    {
        var pgEx = new PostgresException(
            messageText: "duplicate key value violates unique constraint",
            severity: "ERROR",
            invariantSeverity: "ERROR",
            sqlState: sqlState,
            detail: null,
            hint: null,
            position: 0,
            internalPosition: 0,
            internalQuery: null,
            where: null,
            schemaName: null,
            tableName: null,
            columnName: null,
            dataTypeName: null,
            constraintName: constraintName,
            file: null,
            line: null,
            routine: null);
        return new DbUpdateException("DB error", pgEx);
    }

    // ── Fallback path (non-Postgres inner message) ──────────────────────────

    [Test]
    public async Task InvokeAsync_FallbackPath_AccountIdOriginalFilenameConstraint_ReturnsDuplicateOriginalFilenameReason()
    {
        var context = CreateContext();
        var middleware = CreateMiddleware(_ => throw MakeFallbackException(
            "unique constraint violated: IX_videos_account_id_original_filename"));

        await middleware.InvokeAsync(context);

        Assert.That(context.Response.StatusCode, Is.EqualTo(StatusCodes.Status409Conflict));
        var error = await ReadResponseBody(context);
        Assert.That(error, Is.Not.Null);
        Assert.That(error!.Reason, Is.EqualTo("duplicateOriginalFilename"));
    }

    [Test]
    public async Task InvokeAsync_FallbackPath_CategoryIdOriginalFilenameConstraint_ReturnsDuplicateOriginalFilenameReason()
    {
        var context = CreateContext();
        var middleware = CreateMiddleware(_ => throw MakeFallbackException(
            "unique constraint violated: IX_videos_category_id_original_filename"));

        await middleware.InvokeAsync(context);

        Assert.That(context.Response.StatusCode, Is.EqualTo(StatusCodes.Status409Conflict));
        var error = await ReadResponseBody(context);
        Assert.That(error, Is.Not.Null);
        Assert.That(error!.Reason, Is.EqualTo("duplicateOriginalFilename"));
    }

    [Test]
    public async Task InvokeAsync_FallbackPath_CommonUncategorizedOriginalFilenameConstraint_ReturnsDuplicateOriginalFilenameReason()
    {
        var context = CreateContext();
        var middleware = CreateMiddleware(_ => throw MakeFallbackException(
            "unique constraint violated: IX_videos_common_uncategorized_original_filename"));

        await middleware.InvokeAsync(context);

        Assert.That(context.Response.StatusCode, Is.EqualTo(StatusCodes.Status409Conflict));
        var error = await ReadResponseBody(context);
        Assert.That(error, Is.Not.Null);
        Assert.That(error!.Reason, Is.EqualTo("duplicateOriginalFilename"));
    }

    // ── PostgreSQL path ──────────────────────────────────────────────────────

    [Test]
    public async Task InvokeAsync_PostgresPath_AccountIdOriginalFilenameConstraint_ReturnsDuplicateOriginalFilenameReason()
    {
        var context = CreateContext();
        var middleware = CreateMiddleware(_ => throw MakePostgresException(
            "IX_videos_account_id_original_filename"));

        await middleware.InvokeAsync(context);

        Assert.That(context.Response.StatusCode, Is.EqualTo(StatusCodes.Status409Conflict));
        var error = await ReadResponseBody(context);
        Assert.That(error, Is.Not.Null);
        Assert.That(error!.Reason, Is.EqualTo("duplicateOriginalFilename"));
    }

    [Test]
    public async Task InvokeAsync_PostgresPath_CategoryIdOriginalFilenameConstraint_ReturnsDuplicateOriginalFilenameReason()
    {
        var context = CreateContext();
        var middleware = CreateMiddleware(_ => throw MakePostgresException(
            "IX_videos_category_id_original_filename"));

        await middleware.InvokeAsync(context);

        Assert.That(context.Response.StatusCode, Is.EqualTo(StatusCodes.Status409Conflict));
        var error = await ReadResponseBody(context);
        Assert.That(error, Is.Not.Null);
        Assert.That(error!.Reason, Is.EqualTo("duplicateOriginalFilename"));
    }

    [Test]
    public async Task InvokeAsync_PostgresPath_CommonUncategorizedOriginalFilenameConstraint_ReturnsDuplicateOriginalFilenameReason()
    {
        var context = CreateContext();
        var middleware = CreateMiddleware(_ => throw MakePostgresException(
            "IX_videos_common_uncategorized_original_filename"));

        await middleware.InvokeAsync(context);

        Assert.That(context.Response.StatusCode, Is.EqualTo(StatusCodes.Status409Conflict));
        var error = await ReadResponseBody(context);
        Assert.That(error, Is.Not.Null);
        Assert.That(error!.Reason, Is.EqualTo("duplicateOriginalFilename"));
    }
}

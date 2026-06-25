// Copyright (C) 2025-2026 sw.consulting
// This file is a part of Media Pi backend

using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using MediaPi.Core.Middleware;
using MediaPi.Core.RestModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
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
        Assert.That(error!.Reason, Is.EqualTo(ConflictReasons.DuplicateOriginalFilename));
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
        Assert.That(error!.Reason, Is.EqualTo(ConflictReasons.DuplicateOriginalFilename));
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
        Assert.That(error!.Reason, Is.EqualTo(ConflictReasons.DuplicateOriginalFilename));
    }

    [TestCase("IX_videos_account_id_title")]
    [TestCase("IX_videos_category_id_title")]
    [TestCase("IX_videos_common_uncategorized_title")]
    public async Task InvokeAsync_FallbackPath_VideoDescriptionConstraint_ReturnsDuplicateVideoDescriptionReason(string indexName)
    {
        var context = CreateContext();
        var middleware = CreateMiddleware(_ => throw MakeFallbackException(
            $"unique constraint violated: {indexName}"));

        await middleware.InvokeAsync(context);

        Assert.That(context.Response.StatusCode, Is.EqualTo(StatusCodes.Status409Conflict));
        var error = await ReadResponseBody(context);
        Assert.That(error, Is.Not.Null);
        Assert.That(error!.Reason, Is.EqualTo(ConflictReasons.DuplicateVideoDescription));
    }

    [Test]
    public async Task InvokeAsync_FallbackPath_PlaylistDescriptionConstraint_ReturnsDuplicatePlaylistDescriptionReason()
    {
        var context = CreateContext();
        var middleware = CreateMiddleware(_ => throw MakeFallbackException(
            "unique constraint violated: IX_playlists_account_id_title"));

        await middleware.InvokeAsync(context);

        Assert.That(context.Response.StatusCode, Is.EqualTo(StatusCodes.Status409Conflict));
        var error = await ReadResponseBody(context);
        Assert.That(error, Is.Not.Null);
        Assert.That(error!.Reason, Is.EqualTo(ConflictReasons.DuplicatePlaylistDescription));
    }

    [Test]
    public async Task InvokeAsync_FallbackPath_PlaylistFilenameConstraint_ReturnsDuplicatePlaylistFilenameReason()
    {
        var context = CreateContext();
        var middleware = CreateMiddleware(_ => throw MakeFallbackException(
            "unique constraint violated: IX_playlists_account_id_filename"));

        await middleware.InvokeAsync(context);

        Assert.That(context.Response.StatusCode, Is.EqualTo(StatusCodes.Status409Conflict));
        var error = await ReadResponseBody(context);
        Assert.That(error, Is.Not.Null);
        Assert.That(error!.Reason, Is.EqualTo(ConflictReasons.DuplicatePlaylistFilename));
    }

    [Test]
    public async Task InvokeAsync_FallbackPath_UnknownUniqueConstraint_ReturnsGenericUniqueMessage()
    {
        var context = CreateContext();
        var middleware = CreateMiddleware(_ => throw MakeFallbackException(
            "unique constraint violated: IX_unknown_unique_constraint"));

        await middleware.InvokeAsync(context);

        Assert.That(context.Response.StatusCode, Is.EqualTo(StatusCodes.Status409Conflict));
        var error = await ReadResponseBody(context);
        Assert.That(error, Is.Not.Null);
        Assert.That(error!.Msg, Is.EqualTo("Нарушено уникальное ограничение базы данных"));
    }

    [Test]
    public async Task InvokeAsync_FallbackPath_ForeignKeyConstraint_ReturnsForeignKeyMessage()
    {
        var context = CreateContext();
        var middleware = CreateMiddleware(_ => throw MakeFallbackException(
            "foreign key constraint violated"));

        await middleware.InvokeAsync(context);

        Assert.That(context.Response.StatusCode, Is.EqualTo(StatusCodes.Status409Conflict));
        var error = await ReadResponseBody(context);
        Assert.That(error, Is.Not.Null);
        Assert.That(error!.Msg, Is.EqualTo("Нарушено ограничение внешнего ключа базы данных"));
    }

    [Test]
    public async Task InvokeAsync_FallbackPath_OtherConstraint_ReturnsIntegrityMessage()
    {
        var context = CreateContext();
        var middleware = CreateMiddleware(_ => throw MakeFallbackException(
            "check constraint violated"));

        await middleware.InvokeAsync(context);

        Assert.That(context.Response.StatusCode, Is.EqualTo(StatusCodes.Status409Conflict));
        var error = await ReadResponseBody(context);
        Assert.That(error, Is.Not.Null);
        Assert.That(error!.Msg, Is.EqualTo("Нарушено ограничение целостности базы данных"));
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
        Assert.That(error!.Reason, Is.EqualTo(ConflictReasons.DuplicateOriginalFilename));
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
        Assert.That(error!.Reason, Is.EqualTo(ConflictReasons.DuplicateOriginalFilename));
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
        Assert.That(error!.Reason, Is.EqualTo(ConflictReasons.DuplicateOriginalFilename));
    }

    [TestCase("IX_videos_account_id_title")]
    [TestCase("IX_videos_category_id_title")]
    [TestCase("IX_videos_common_uncategorized_title")]
    public async Task InvokeAsync_PostgresPath_VideoDescriptionConstraint_ReturnsDuplicateVideoDescriptionReason(string indexName)
    {
        var context = CreateContext();
        var middleware = CreateMiddleware(_ => throw MakePostgresException(indexName));

        await middleware.InvokeAsync(context);

        Assert.That(context.Response.StatusCode, Is.EqualTo(StatusCodes.Status409Conflict));
        var error = await ReadResponseBody(context);
        Assert.That(error, Is.Not.Null);
        Assert.That(error!.Reason, Is.EqualTo(ConflictReasons.DuplicateVideoDescription));
    }

    [Test]
    public async Task InvokeAsync_PostgresPath_PlaylistDescriptionConstraint_ReturnsDuplicatePlaylistDescriptionReason()
    {
        var context = CreateContext();
        var middleware = CreateMiddleware(_ => throw MakePostgresException(
            "IX_playlists_account_id_title"));

        await middleware.InvokeAsync(context);

        Assert.That(context.Response.StatusCode, Is.EqualTo(StatusCodes.Status409Conflict));
        var error = await ReadResponseBody(context);
        Assert.That(error, Is.Not.Null);
        Assert.That(error!.Reason, Is.EqualTo(ConflictReasons.DuplicatePlaylistDescription));
    }

    [Test]
    public async Task InvokeAsync_PostgresPath_PlaylistFilenameConstraint_ReturnsDuplicatePlaylistFilenameReason()
    {
        var context = CreateContext();
        var middleware = CreateMiddleware(_ => throw MakePostgresException(
            "IX_playlists_account_id_filename"));

        await middleware.InvokeAsync(context);

        Assert.That(context.Response.StatusCode, Is.EqualTo(StatusCodes.Status409Conflict));
        var error = await ReadResponseBody(context);
        Assert.That(error, Is.Not.Null);
        Assert.That(error!.Reason, Is.EqualTo(ConflictReasons.DuplicatePlaylistFilename));
    }

    [Test]
    public async Task InvokeAsync_PostgresPath_PlaylistDeviceGroupConstraint_ReturnsPlaylistDeviceGroupMessage()
    {
        var context = CreateContext();
        var middleware = CreateMiddleware(_ => throw MakePostgresException(
            "IX_playlist_device_group_device_group_id"));

        await middleware.InvokeAsync(context);

        Assert.That(context.Response.StatusCode, Is.EqualTo(StatusCodes.Status409Conflict));
        var error = await ReadResponseBody(context);
        Assert.That(error, Is.Not.Null);
        Assert.That(error!.Msg, Is.EqualTo("Группа устройств может иметь не более одного проигрываемого плейлиста"));
    }

    [Test]
    public async Task InvokeAsync_PostgresPath_UnknownUniqueConstraint_ReturnsGenericUniqueMessage()
    {
        var context = CreateContext();
        var middleware = CreateMiddleware(_ => throw MakePostgresException(
            "IX_unknown_unique_constraint"));

        await middleware.InvokeAsync(context);

        Assert.That(context.Response.StatusCode, Is.EqualTo(StatusCodes.Status409Conflict));
        var error = await ReadResponseBody(context);
        Assert.That(error, Is.Not.Null);
        Assert.That(error!.Msg, Is.EqualTo("Нарушено уникальное ограничение базы данных"));
    }

    [TestCase("23503", "Нарушено ограничение внешнего ключа базы данных")]
    [TestCase("23514", "Данные не соответствуют ограничениям на значения полей")]
    [TestCase("23001", "Нарушено ограничение целостности базы данных")]
    [TestCase("23000", "Нарушено ограничение целостности базы данных")]
    public async Task InvokeAsync_PostgresPath_NonUniqueConstraint_ReturnsMappedMessage(string sqlState, string expectedMessage)
    {
        var context = CreateContext();
        var middleware = CreateMiddleware(_ => throw MakePostgresException(
            "constraint_name", sqlState));

        await middleware.InvokeAsync(context);

        Assert.That(context.Response.StatusCode, Is.EqualTo(StatusCodes.Status409Conflict));
        var error = await ReadResponseBody(context);
        Assert.That(error, Is.Not.Null);
        Assert.That(error!.Msg, Is.EqualTo(expectedMessage));
    }

    [Test]
    public void InvokeAsync_ResponseAlreadyStarted_RethrowsOriginalException()
    {
        var context = new DefaultHttpContext();
        context.Features.Set<IHttpResponseFeature>(new StartedResponseFeature());
        var middleware = CreateMiddleware(_ => throw MakeFallbackException(
            "unique constraint violated: IX_unknown_unique_constraint"));

        var ex = Assert.ThrowsAsync<DbUpdateException>(async () => await middleware.InvokeAsync(context));

        Assert.That(ex, Is.Not.Null);
        Assert.That(context.Response.HasStarted, Is.True);
    }

    private sealed class StartedResponseFeature : IHttpResponseFeature
    {
        public int StatusCode { get; set; } = StatusCodes.Status200OK;
        public string? ReasonPhrase { get; set; }
        public IHeaderDictionary Headers { get; set; } = new HeaderDictionary();
        public Stream Body { get; set; } = new MemoryStream();
        public bool HasStarted => true;

        public void OnCompleted(Func<object, Task> callback, object state)
        {
        }

        public void OnStarting(Func<object, Task> callback, object state)
        {
        }
    }
}

// Copyright (C) 2025-2026 sw.consulting
// This file is a part of Media Pi backend

using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

using MediaPi.Core.Middleware;
using MediaPi.Core.RestModels;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace MediaPi.Core.Tests.Middleware;

[TestFixture]
public class UploadExceptionMiddlewareTests
{
    [Test]
    public async Task Invoke_VideoUploadTooLarge_ReturnsStructured413()
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/videos/upload";
        context.Response.Body = new MemoryStream();
        var middleware = new UploadExceptionMiddleware(
            _ => throw new InvalidDataException("Multipart body length limit exceeded."),
            Mock.Of<ILogger<UploadExceptionMiddleware>>());

        await middleware.Invoke(context);

        Assert.That(context.Response.StatusCode, Is.EqualTo(StatusCodes.Status413PayloadTooLarge));
        var error = await ReadResponseBody(context);
        Assert.That(error, Is.Not.Null);
        Assert.That(error!.Msg, Is.EqualTo("Размер загружаемого файла превышает допустимый предел"));
        Assert.That(error.Reason, Is.EqualTo(ConflictReasons.VideoUploadTooLarge));
    }

    private static async Task<ErrMessage?> ReadResponseBody(HttpContext context)
    {
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        return await JsonSerializer.DeserializeAsync<ErrMessage>(
            context.Response.Body,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    }
}

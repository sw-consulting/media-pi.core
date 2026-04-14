// Copyright (C) 2025-2026 sw.consulting
// This file is a part of Media Pi backend

using System;
using System.IO;
using System.Threading.Tasks;
using MediaPi.Core.Authorization;
using MediaPi.Core.Data;
using MediaPi.Core.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Moq;
using NUnit.Framework;

namespace MediaPi.Core.Tests.Authorization;

/// <summary>
/// Tests for AuthorizeDeviceByXIdMiddleware.
/// Devices are authorized via X-Device-Id header containing server_key.
/// </summary>
public class AuthorizeDeviceByXIdMiddlewareTests
{
    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static DefaultHttpContext CreateContextWithEndpoint(params object[] metadata)
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        if (metadata.Length > 0)
        {
            var endpoint = new Endpoint(_ => Task.CompletedTask, new EndpointMetadataCollection(metadata), "test");
            context.SetEndpoint(endpoint);
        }

        return context;
    }

    [Test]
    public async Task Invoke_SkipsAuthorization_WhenAttributeMissing()
    {
        using var db = CreateDbContext();
        var context = new DefaultHttpContext();
        var nextCalled = false;
        var middleware = new AuthorizeDeviceByXIdMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.Invoke(context, db, Mock.Of<ILogger<AuthorizeDeviceByXIdMiddleware>>());

        Assert.That(nextCalled, Is.True);
        Assert.That(context.Response.StatusCode, Is.EqualTo(StatusCodes.Status200OK));
    }

    [Test]
    public async Task Invoke_SkipsAuthorization_WhenAllowAnonymousPresent()
    {
        using var db = CreateDbContext();
        var context = CreateContextWithEndpoint(new AuthorizeDeviceAttribute(), new Core.Authorization.AllowAnonymousAttribute());
        var nextCalled = false;
        var middleware = new AuthorizeDeviceByXIdMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.Invoke(context, db, Mock.Of<ILogger<AuthorizeDeviceByXIdMiddleware>>());

        Assert.That(nextCalled, Is.True);
        Assert.That(context.Response.StatusCode, Is.EqualTo(StatusCodes.Status200OK));
    }

    [Test]
    public async Task Invoke_ReturnsUnauthorized_WhenXDeviceIdHeaderMissing()
    {
        using var db = CreateDbContext();
        var context = CreateContextWithEndpoint(new AuthorizeDeviceAttribute());
        var nextCalled = false;
        var middleware = new AuthorizeDeviceByXIdMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.Invoke(context, db, Mock.Of<ILogger<AuthorizeDeviceByXIdMiddleware>>());

        Assert.That(nextCalled, Is.False);
        Assert.That(context.Response.StatusCode, Is.EqualTo(StatusCodes.Status401Unauthorized));

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(context.Response.Body);
        var responseBody = await reader.ReadToEndAsync();
        Assert.That(responseBody, Does.Contain("X-Device-Id"));
    }

    [Test]
    public async Task Invoke_ReturnsUnauthorized_WhenXDeviceIdHeaderEmpty()
    {
        using var db = CreateDbContext();
        var context = CreateContextWithEndpoint(new AuthorizeDeviceAttribute());
        context.Request.Headers["X-Device-Id"] = new StringValues(string.Empty);
        var nextCalled = false;
        var middleware = new AuthorizeDeviceByXIdMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.Invoke(context, db, Mock.Of<ILogger<AuthorizeDeviceByXIdMiddleware>>());

        Assert.That(nextCalled, Is.False);
        Assert.That(context.Response.StatusCode, Is.EqualTo(StatusCodes.Status401Unauthorized));

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(context.Response.Body);
        var responseBody = await reader.ReadToEndAsync();
        Assert.That(responseBody, Does.Contain("пуст"));
    }

    [Test]
    public async Task Invoke_ReturnsUnauthorized_WhenDeviceNotFound()
    {
        using var db = CreateDbContext();
        var context = CreateContextWithEndpoint(new AuthorizeDeviceAttribute());
        context.Request.Headers["X-Device-Id"] = new StringValues("unknown-server-key");
        var nextCalled = false;
        var middleware = new AuthorizeDeviceByXIdMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.Invoke(context, db, Mock.Of<ILogger<AuthorizeDeviceByXIdMiddleware>>());

        Assert.That(nextCalled, Is.False);
        Assert.That(context.Response.StatusCode, Is.EqualTo(StatusCodes.Status401Unauthorized));

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(context.Response.Body);
        var responseBody = await reader.ReadToEndAsync();
        Assert.That(responseBody, Does.Contain("не найдено"));
    }

    [Test]
    public async Task Invoke_AttachesDeviceId_WhenServerKeyMatches()
    {
        using var db = CreateDbContext();
        db.Devices.Add(new Device
        {
            Id = 42,
            Name = "TestDevice",
            IpAddress = "10.0.0.10",
            Port = 8081,
            ServerKey = "device-server-key-123"
        });
        await db.SaveChangesAsync();

        var context = CreateContextWithEndpoint(new AuthorizeDeviceAttribute());
        context.Request.Headers["X-Device-Id"] = new StringValues("device-server-key-123");
        var nextCalled = false;
        var middleware = new AuthorizeDeviceByXIdMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.Invoke(context, db, Mock.Of<ILogger<AuthorizeDeviceByXIdMiddleware>>());

        Assert.That(nextCalled, Is.True);
        Assert.That(context.Items["DeviceId"], Is.EqualTo(42));
    }

    [Test]
    public async Task Invoke_TrimsWhitespace_FromServerKey()
    {
        using var db = CreateDbContext();
        db.Devices.Add(new Device
        {
            Id = 99,
            Name = "TestDevice",
            IpAddress = "10.0.0.20",
            Port = 8081,
            ServerKey = "my-key"
        });
        await db.SaveChangesAsync();

        var context = CreateContextWithEndpoint(new AuthorizeDeviceAttribute());
        context.Request.Headers["X-Device-Id"] = new StringValues("  my-key  ");
        var nextCalled = false;
        var middleware = new AuthorizeDeviceByXIdMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.Invoke(context, db, Mock.Of<ILogger<AuthorizeDeviceByXIdMiddleware>>());

        Assert.That(nextCalled, Is.True);
        Assert.That(context.Items["DeviceId"], Is.EqualTo(99));
    }
}


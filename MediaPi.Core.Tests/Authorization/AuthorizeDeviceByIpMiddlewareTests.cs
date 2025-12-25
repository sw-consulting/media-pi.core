// Copyright (c) 2025 sw.consulting
// This file is a part of Media Pi backend

using System;
using System.Net;
using System.Threading.Tasks;
using MediaPi.Core.Authorization;
using MediaPi.Core.Data;
using MediaPi.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace MediaPi.Core.Tests.Authorization;

public class AuthorizeDeviceByIpMiddlewareTests
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
        context.Features.Set<IHttpResponseBodyFeature>(new StreamResponseBodyFeature(new System.IO.MemoryStream()));
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
        var middleware = new AuthorizeDeviceByIpMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.Invoke(context, db, Mock.Of<ILogger<AuthorizeDeviceByIpMiddleware>>());

        Assert.That(nextCalled, Is.True);
        Assert.That(context.Response.StatusCode, Is.EqualTo(StatusCodes.Status200OK));
    }

    [Test]
    public async Task Invoke_SkipsAuthorization_WhenAllowAnonymousPresent()
    {
        using var db = CreateDbContext();
        var context = CreateContextWithEndpoint(new AuthorizeDeviceAttribute(), new AllowAnonymousAttribute());
        var nextCalled = false;
        var middleware = new AuthorizeDeviceByIpMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.Invoke(context, db, Mock.Of<ILogger<AuthorizeDeviceByIpMiddleware>>());

        Assert.That(nextCalled, Is.True);
        Assert.That(context.Response.StatusCode, Is.EqualTo(StatusCodes.Status200OK));
    }

    [Test]
    public async Task Invoke_ReturnsUnauthorized_WhenIpMissing()
    {
        using var db = CreateDbContext();
        var context = CreateContextWithEndpoint(new AuthorizeDeviceAttribute());
        var nextCalled = false;
        var middleware = new AuthorizeDeviceByIpMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.Invoke(context, db, Mock.Of<ILogger<AuthorizeDeviceByIpMiddleware>>());

        Assert.That(nextCalled, Is.False);
        Assert.That(context.Response.StatusCode, Is.EqualTo(StatusCodes.Status401Unauthorized));
    }

    [Test]
    public async Task Invoke_ReturnsUnauthorized_WhenDeviceNotFound()
    {
        using var db = CreateDbContext();
        var context = CreateContextWithEndpoint(new AuthorizeDeviceAttribute());
        context.Connection.RemoteIpAddress = IPAddress.Parse("10.0.0.5");
        var nextCalled = false;
        var middleware = new AuthorizeDeviceByIpMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.Invoke(context, db, Mock.Of<ILogger<AuthorizeDeviceByIpMiddleware>>());

        Assert.That(nextCalled, Is.False);
        Assert.That(context.Response.StatusCode, Is.EqualTo(StatusCodes.Status401Unauthorized));
    }

    [Test]
    public async Task Invoke_AttachesDeviceId_WhenDeviceFound()
    {
        using var db = CreateDbContext();
        db.Devices.Add(new Device
        {
            Id = 11,
            Name = "Device",
            IpAddress = "10.0.0.10",
            Port = 8081,
            ServerKey = "key"
        });
        await db.SaveChangesAsync();

        var context = CreateContextWithEndpoint(new AuthorizeDeviceAttribute());
        context.Connection.RemoteIpAddress = IPAddress.Parse("10.0.0.10");
        var nextCalled = false;
        var middleware = new AuthorizeDeviceByIpMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.Invoke(context, db, Mock.Of<ILogger<AuthorizeDeviceByIpMiddleware>>());

        Assert.That(nextCalled, Is.True);
        Assert.That(context.Items["DeviceId"], Is.EqualTo(11));
    }
}

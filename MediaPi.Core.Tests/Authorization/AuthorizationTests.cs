// Copyright (C) 2025 Maxim [maxirmx] Samsonov (www.sw.consulting)
// All rights reserved.
// This file is a part of Fuelflux Core application
//
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions
// are met:
// 1. Redistributions of source code must retain the above copyright
// notice, this list of conditions and the following disclaimer.
// 2. Redistributions in binary form must reproduce the above copyright
// notice, this list of conditions and the following disclaimer in the
// documentation and/or other materials provided with the distribution.
//
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
// ``AS IS'' AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED
// TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR
// PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDERS OR CONTRIBUTORS
// BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR
// CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF
// SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS
// INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN
// CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE)
// ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE
// POSSIBILITY OF SUCH DAMAGE.

using NUnit.Framework;
using Moq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Security.Claims;
using System.Text;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;
using System.Security.Cryptography;

using Fuelflux.Core.Authorization;
using Fuelflux.Core.Models;
using Fuelflux.Core.Settings;
using Fuelflux.Core.RestModels;
using System.Threading.Tasks;
using System;
using System.Linq;

namespace Fuelflux.Core.Tests.Authorization;

public class AuthorizeAttributeTests
{
    private AuthorizationFilterContext CreateContext(IDictionary<object, object?> items, bool allowAnonymous = false)
    {
        var httpContext = new Mock<HttpContext>();
        httpContext.Setup(h => h.Items).Returns(items);

        var actionDescriptor = new ActionDescriptor();
        if (allowAnonymous)
        {
            actionDescriptor.EndpointMetadata = new[] { new AllowAnonymousAttribute() };
        }
        else
        {
            actionDescriptor.EndpointMetadata = Array.Empty<object>();
        }

        var filters = new List<IFilterMetadata>();
        var mockServiceProvider = new Mock<IServiceProvider>();
        var mockLogger = new Mock<ILogger<AuthorizeAttribute>>();
        mockServiceProvider
            .Setup(x => x.GetService(typeof(ILogger<AuthorizeAttribute>)))
            .Returns(mockLogger.Object);

        httpContext.Setup(h => h.RequestServices).Returns(mockServiceProvider.Object);

        var context = new AuthorizationFilterContext(
            new ActionContext(
                httpContext.Object,
                new RouteData(),
                actionDescriptor
            ),
            filters
        );

        return context;
    }

    [Test]
    public void OnAuthorization_SkipsAuthorization_WhenAllowAnonymousAttributePresent()
    {
        // Arrange
        var items = new Dictionary<object, object?>();
        var context = CreateContext(items, allowAnonymous: true);
        var attribute = new AuthorizeAttribute();

        // Act
        attribute.OnAuthorization(context);

        // Assert
        Assert.That(context.Result, Is.Null);
    }

    [Test]
    public void OnAuthorization_ReturnsUnauthorized_WhenUserIdIsNull()
    {
        // Arrange
        var items = new Dictionary<object, object?>();
        var context = CreateContext(items);
        var attribute = new AuthorizeAttribute();

        // Act
        attribute.OnAuthorization(context);

        // Assert
        Assert.That(context.Result, Is.Not.Null);
        Assert.That(context.Result, Is.TypeOf<JsonResult>());

        var jsonResult = context.Result as JsonResult;
        Assert.That(jsonResult!.StatusCode, Is.EqualTo(StatusCodes.Status401Unauthorized));
        Assert.That(jsonResult.Value, Is.TypeOf<ErrMessage>());
    }

    [Test]
    public void OnAuthorization_AllowsAccess_WhenUserIdExists()
    {
        // Arrange
        var items = new Dictionary<object, object?>
        {
            ["UserId"] = 1
        };
        var context = CreateContext(items);
        var attribute = new AuthorizeAttribute();

        // Act
        attribute.OnAuthorization(context);

        // Assert
        Assert.That(context.Result, Is.Null);
    }
}

public class JwtUtilsTests
{
    private readonly AppSettings _appSettings;
    private readonly Mock<ILogger<JwtUtils>> _mockLogger;
    private readonly JwtUtils _jwtUtils;
    private readonly User _testUser;

    public JwtUtilsTests()
    {
        _appSettings = new AppSettings
        {
            Secret = "ThisIsAVeryLongSecretKeyForTestingPurposesOnlyDoNotUseInProduction",
            JwtTokenExpirationDays = 7
        };
        var mockOptions = new Mock<IOptions<AppSettings>>();
        mockOptions.Setup(x => x.Value).Returns(_appSettings);

        _mockLogger = new Mock<ILogger<JwtUtils>>();

        _jwtUtils = new JwtUtils(mockOptions.Object, _mockLogger.Object);

        _testUser = new User
        {
            Id = 123,
            Email = "test@example.com",
            Password = "password"
        };
    }

    [Test]
    public void GenerateJwtToken_ReturnsValidToken_WithUserId()
    {
        // Act
        var token = _jwtUtils.GenerateJwtToken(_testUser);

        // Assert
        Assert.That(token, Is.Not.Null);
        Assert.That(token, Is.Not.Empty);
    }

    [Test]
    public void ValidateJwtToken_ReturnsUserId_WhenTokenIsValid()
    {
        // Arrange
        var token = _jwtUtils.GenerateJwtToken(_testUser);

        // Act
        var result = _jwtUtils.ValidateJwtToken(token);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result, Is.EqualTo(_testUser.Id));
    }

    [Test]
    public void ValidateJwtToken_ReturnsNull_WhenTokenIsNull()
    {
        // Act
        var result = _jwtUtils.ValidateJwtToken(null);

        // Assert
        Assert.That(result, Is.Null);
    }

    [Test]
    public void ValidateJwtToken_ReturnsNull_WhenTokenIsInvalid()
    {
        // Act
        var result = _jwtUtils.ValidateJwtToken("invalid.token.string");

        // Assert
        Assert.That(result, Is.Null);
    }

    [Test]
    public void Constructor_ThrowsException_WhenSecretIsEmpty()
    {
        // Arrange
        var emptyAppSettings = new AppSettings { Secret = "" };
        var mockOptions = new Mock<IOptions<AppSettings>>();
        mockOptions.Setup(x => x.Value).Returns(emptyAppSettings);

        // Act & Assert
        Assert.Throws<Exception>(() => new JwtUtils(mockOptions.Object, _mockLogger.Object));
    }

    [Test]
    public void GenerateJwtToken_SetsExpirationCorrectly()
    {
        // Arrange
        _appSettings.JwtTokenExpirationDays = 3;
        var before = DateTime.UtcNow;

        // Act
        var token = _jwtUtils.GenerateJwtToken(_testUser);

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);
        Assert.That(jwt.ValidTo, Is.EqualTo(before.AddDays(3)).Within(TimeSpan.FromSeconds(5)));
    }

    [Test]
    public void ValidateJwtToken_ReturnsNull_WhenTokenIsExpired()
    {
        // Arrange
        var handler = new JwtSecurityTokenHandler();
        var key = SHA256.HashData(Encoding.UTF8.GetBytes(_appSettings.Secret!));
        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[] { new Claim("id", _testUser.Id.ToString()) }),
            NotBefore = DateTime.UtcNow.AddMinutes(-5),
            Expires = DateTime.UtcNow.AddSeconds(-1),
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
        };
        var token = handler.CreateToken(descriptor);
        var tokenString = handler.WriteToken(token);

        // Act
        var result = _jwtUtils.ValidateJwtToken(tokenString);

        // Assert
        Assert.That(result, Is.Null);
    }

    [Test]
    public void ValidateJwtToken_ReturnsNull_WhenTokenIsMalformed()
    {
        // Act
        var result = _jwtUtils.ValidateJwtToken("notatoken");

        // Assert
        Assert.That(result, Is.Null);
    }

    [Test]
    public void ValidateJwtToken_ReturnsNull_WhenSecretDoesNotMatch()
    {
        // Arrange
        var token = _jwtUtils.GenerateJwtToken(_testUser);
        _appSettings.Secret = "AnotherSecretThatDoesNotMatch";

        // Act
        var result = _jwtUtils.ValidateJwtToken(token);

        // Assert
        Assert.That(result, Is.Null);
    }
}

public class JwtMiddlewareTests
{
    [Test]
    public async Task Invoke_SetsUserIdInContext_WhenTokenIsValid()
    {
        // Arrange
        var userId = 123;
        var context = new DefaultHttpContext();
        context.Request.Headers["Authorization"] = "Bearer validToken";

        var mockJwtUtils = new Mock<IJwtUtils>();
        mockJwtUtils.Setup(x => x.ValidateJwtToken("validToken")).Returns(userId);

        var middleware = new JwtMiddleware(innerContext => Task.CompletedTask);

        // Act
        await middleware.Invoke(context, mockJwtUtils.Object);

        // Assert
        Assert.That(context.Items["UserId"], Is.EqualTo(userId));
    }

    [Test]
    public async Task Invoke_DoesNotSetUserId_WhenTokenIsInvalid()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Headers["Authorization"] = "Bearer invalidToken";

        var mockJwtUtils = new Mock<IJwtUtils>();
        mockJwtUtils.Setup(x => x.ValidateJwtToken("invalidToken")).Returns((int?)null);

        var middleware = new JwtMiddleware(innerContext => Task.CompletedTask);

        // Act
        await middleware.Invoke(context, mockJwtUtils.Object);

        // Assert
        Assert.That(context.Items.ContainsKey("UserId"), Is.False);
    }

    [Test]
    public async Task Invoke_DoesNotSetUserId_WhenNoAuthorizationHeader()
    {
        // Arrange
        var context = new DefaultHttpContext();

        var mockJwtUtils = new Mock<IJwtUtils>();
        var middleware = new JwtMiddleware(innerContext => Task.CompletedTask);

        // Act
        await middleware.Invoke(context, mockJwtUtils.Object);

        // Assert
        Assert.That(context.Items.ContainsKey("UserId"), Is.False);
    }

    [Test]
    public async Task Invoke_CallsNextDelegate()
    {
        // Arrange
        var context = new DefaultHttpContext();
        var nextDelegateCalled = false;

        var mockJwtUtils = new Mock<IJwtUtils>();
        var nextDelegate = new RequestDelegate(_ => {
            nextDelegateCalled = true;
            return Task.CompletedTask;
        });

        var middleware = new JwtMiddleware(nextDelegate);

        // Act
        await middleware.Invoke(context, mockJwtUtils.Object);

        // Assert
        Assert.That(nextDelegateCalled, Is.True);
    }
}

[TestFixture]
public class AllowAnonymousAttributeTests
{
    [Test]
    public void AllowAnonymousAttribute_CanBeAppliedToMethod()
    {
        // This test verifies that the AllowAnonymousAttribute can be applied to a method
        var attribute = new AllowAnonymousAttribute();

        // Assert it exists and is of the correct type
        Assert.That(attribute, Is.InstanceOf<Attribute>());

        // Verify it can only be applied to methods
        var usageAttribute = typeof(AllowAnonymousAttribute).GetCustomAttributes(typeof(AttributeUsageAttribute), false)
            .Cast<AttributeUsageAttribute>()
            .FirstOrDefault();

        Assert.That(usageAttribute, Is.Not.Null);
        Assert.That(usageAttribute!.ValidOn, Is.EqualTo(AttributeTargets.Method));
    }
}

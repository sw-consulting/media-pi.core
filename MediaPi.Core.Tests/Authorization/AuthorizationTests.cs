// Copyright (c) 2025 sw.consulting
// This file is a part of Media Pi backend

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

using MediaPi.Core.Authorization;
using MediaPi.Core.Models;
using MediaPi.Core.Settings;
using MediaPi.Core.RestModels;
using System.Threading.Tasks;
using System;
using System.Linq;

namespace MediaPi.Core.Tests.Authorization;

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

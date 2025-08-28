// Developed by Maxim [maxirmx] Samsonov (www.sw.consulting)
// This file is a part of Media Pi backend application

using System;
using MediaPi.Core.Authorization;
using MediaPi.Core.Controllers;
using MediaPi.Core.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace MediaPi.Core.Tests.Controllers;

[TestFixture]
public class DeviceStatusesControllerAuthorizationTests
{
#pragma warning disable CS8618
    private Mock<IDeviceMonitoringService> _monitoringServiceMock;
    private DeviceStatusesController _controller;
    private DefaultHttpContext _httpContext;
    private Mock<IServiceProvider> _serviceProviderMock;
    private Mock<ILogger<AuthorizeAttribute>> _loggerMock;
#pragma warning restore CS8618

    [SetUp]
    public void SetUp()
    {
        _monitoringServiceMock = new Mock<IDeviceMonitoringService>();
        _controller = new DeviceStatusesController(_monitoringServiceMock.Object);
        
        _httpContext = new DefaultHttpContext();
        _serviceProviderMock = new Mock<IServiceProvider>();
        _loggerMock = new Mock<ILogger<AuthorizeAttribute>>();
        
        _serviceProviderMock
            .Setup(x => x.GetService(typeof(ILogger<AuthorizeAttribute>)))
            .Returns(_loggerMock.Object);
            
        _httpContext.RequestServices = _serviceProviderMock.Object;
        
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = _httpContext
        };
    }

    [Test]
    public void AuthorizeAttribute_AllowsAccess_WhenUserIdExists()
    {
        // Arrange
        var context = CreateAuthorizationContext(userId: 1);
        var attribute = new AuthorizeAttribute();

        // Act
        attribute.OnAuthorization(context);

        // Assert
        Assert.That(context.Result, Is.Null, "Authorization should succeed when UserId is present");
    }

    [Test]
    public void AuthorizeAttribute_DeniesAccess_WhenUserIdMissing()
    {
        // Arrange
        var context = CreateAuthorizationContext(userId: null);
        var attribute = new AuthorizeAttribute();

        // Act
        attribute.OnAuthorization(context);

        // Assert
        Assert.That(context.Result, Is.Not.Null, "Authorization should fail when UserId is missing");
        Assert.That(context.Result, Is.TypeOf<JsonResult>());
        
        var jsonResult = context.Result as JsonResult;
        Assert.That(jsonResult!.StatusCode, Is.EqualTo(StatusCodes.Status401Unauthorized));
    }

    [Test]
    public void AuthorizeAttribute_SkipsAuthorization_WhenAllowAnonymousPresent()
    {
        // Arrange
        var context = CreateAuthorizationContext(userId: null, allowAnonymous: true);
        var attribute = new AuthorizeAttribute();

        // Act
        attribute.OnAuthorization(context);

        // Assert
        Assert.That(context.Result, Is.Null, "Authorization should be skipped when AllowAnonymous is present");
    }

    [Test]
    public void DeviceStatusesController_HasAuthorizeAttribute()
    {
        // Arrange & Act
        var controllerType = typeof(DeviceStatusesController);
        var attributes = controllerType.GetCustomAttributes(typeof(AuthorizeAttribute), false);

        // Assert
        Assert.That(attributes.Length, Is.EqualTo(1), "DeviceStatusesController should have [Authorize] attribute");
    }

    [Test]
    public void StreamMethod_InheritsAuthorizationFromController()
    {
        // Arrange & Act
        var streamMethod = typeof(DeviceStatusesController).GetMethod("Stream");
        var allowAnonymousAttributes = streamMethod?.GetCustomAttributes(typeof(AllowAnonymousAttribute), false);
        var authorizeAttributes = streamMethod?.GetCustomAttributes(typeof(AuthorizeAttribute), false);

        // Assert
        Assert.That(streamMethod, Is.Not.Null, "Stream method should exist");
        Assert.That(allowAnonymousAttributes?.Length ?? 0, Is.EqualTo(0), "Stream method should not have [AllowAnonymous]");
        Assert.That(authorizeAttributes?.Length ?? 0, Is.EqualTo(0), "Stream method should not have its own [Authorize] (inherits from controller)");
    }

    private AuthorizationFilterContext CreateAuthorizationContext(int? userId, bool allowAnonymous = false)
    {
        var actionDescriptor = new Microsoft.AspNetCore.Mvc.Abstractions.ActionDescriptor();
        
        if (allowAnonymous)
        {
            actionDescriptor.EndpointMetadata = new object[] { new AllowAnonymousAttribute() };
        }
        else
        {
            actionDescriptor.EndpointMetadata = System.Array.Empty<object>();
        }

        var httpContext = new DefaultHttpContext();
        httpContext.RequestServices = _serviceProviderMock.Object;
        
        if (userId.HasValue)
        {
            httpContext.Items["UserId"] = userId.Value;
        }

        var actionContext = new ActionContext(
            httpContext,
            new Microsoft.AspNetCore.Routing.RouteData(),
            actionDescriptor
        );

        return new AuthorizationFilterContext(
            actionContext,
            new System.Collections.Generic.List<Microsoft.AspNetCore.Mvc.Filters.IFilterMetadata>()
        );
    }
}
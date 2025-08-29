// Developed by Maxim [maxirmx] Samsonov (www.sw.consulting)
// This file is a part of Media Pi backend application

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediaPi.Core.Controllers;
using MediaPi.Core.RestModels;
using MediaPi.Core.Services;
using MediaPi.Core.Services.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using NUnit.Framework;

namespace MediaPi.Core.Tests.Controllers;

[TestFixture]
public class DeviceStatusesControllerTests
{
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor.
    private Mock<IDeviceMonitoringService> _monitoringServiceMock;
    private DeviceStatusesController _controller;
    private DefaultHttpContext _httpContext;
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor.

    [SetUp]
    public void SetUp()
    {
        _monitoringServiceMock = new Mock<IDeviceMonitoringService>();
        _controller = new DeviceStatusesController(_monitoringServiceMock.Object);
        
        // Set up HttpContext for testing Stream endpoint
        _httpContext = new DefaultHttpContext();
        _httpContext.Response.Body = new MemoryStream();
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = _httpContext
        };
    }

    [Test]
    public void GetAll_ReturnsAllDeviceStatuses()
    {
        var snapshot = new Dictionary<int, DeviceStatusSnapshot>
        {
            { 1, new DeviceStatusSnapshot { IpAddress = "192.168.1.10", IsOnline = true, LastChecked = DateTime.UtcNow, ConnectLatencyMs = 10, TotalLatencyMs = 20 } },
            { 2, new DeviceStatusSnapshot { IpAddress = "192.168.1.11", IsOnline = false, LastChecked = DateTime.UtcNow, ConnectLatencyMs = 30, TotalLatencyMs = 40 } }
        };
        _monitoringServiceMock.Setup(s => s.Snapshot).Returns(snapshot);

        var result = _controller.GetAll();
        Assert.That(result.Result, Is.TypeOf<OkObjectResult>());
        var okResult = result.Result as OkObjectResult;
        Assert.That(okResult?.Value, Is.Not.Null);
        if (okResult?.Value is IEnumerable<DeviceStatusItem> value)
        {
            var list = value.ToList();
            Assert.That(list.Count, Is.EqualTo(2));
            Assert.That(list[0].DeviceId, Is.EqualTo(1));
            Assert.That(list[1].DeviceId, Is.EqualTo(2));
        }
    }

    [Test]
    public void GetAll_ReturnsEmptyList_WhenNoDevices()
    {
        _monitoringServiceMock.Setup(s => s.Snapshot).Returns(new Dictionary<int, DeviceStatusSnapshot>());
        var result = _controller.GetAll();
        Assert.That(result.Result, Is.TypeOf<OkObjectResult>());
        var okResult = result.Result as OkObjectResult;
        Assert.That(okResult?.Value, Is.Not.Null);
        if (okResult?.Value is IEnumerable<DeviceStatusItem> value)
        {
            Assert.That(value.ToList(), Is.Empty);
        }
    }

    [Test]
    public async Task Get_ReturnsDeviceStatus_WhenFound()
    {
        var snapshot = new DeviceStatusSnapshot
        {
            IpAddress = "192.168.1.10",
            IsOnline = true,
            LastChecked = DateTime.UtcNow,
            ConnectLatencyMs = 10,
            TotalLatencyMs = 20
        };
        _monitoringServiceMock.Setup(s => s.Test(1, It.IsAny<CancellationToken>())).ReturnsAsync(snapshot);
        var result = await _controller.Get(1);
        Assert.That(result.Result, Is.TypeOf<OkObjectResult>());
        var okResult = result.Result as OkObjectResult;
        Assert.That(okResult?.Value, Is.Not.Null);
        if (okResult?.Value is DeviceStatusItem returnedItem)
        {
            Assert.That(returnedItem.DeviceId, Is.EqualTo(1));
            Assert.That(returnedItem.IsOnline, Is.True);
        }
    }

    public async Task Get_ReturnsNotFound_WhenDeviceMissing()
    {
        _monitoringServiceMock.Setup(s => s.Test(99, It.IsAny<CancellationToken>())).ReturnsAsync((DeviceStatusSnapshot?)null);
        var result = await _controller.Get(99);
        Assert.That(result.Result, Is.TypeOf<NotFoundObjectResult>());
        var notFound = result.Result as NotFoundObjectResult;
        Assert.That(notFound?.Value, Is.Not.Null);
        if (notFound?.Value != null)
        {
            Assert.That(notFound.Value, Is.TypeOf<ErrMessage>());
            var err = notFound.Value as ErrMessage;
            Assert.That(err?.Msg, Does.Contain("99"));
        }
    }

    [Test]
    public async Task Test_ReturnsDeviceStatus_WhenFound()
    {
        var snapshot = new DeviceStatusSnapshot
        {
            IpAddress = "192.168.1.10",
            IsOnline = true,
            LastChecked = DateTime.UtcNow,
            ConnectLatencyMs = 10,
            TotalLatencyMs = 20
        };
        _monitoringServiceMock.Setup(s => s.Test(1, It.IsAny<CancellationToken>())).ReturnsAsync(snapshot);
        var result = await _controller.Test(1);
        Assert.That(result.Result, Is.TypeOf<OkObjectResult>());
        var okResult = result.Result as OkObjectResult;
        Assert.That(okResult?.Value, Is.Not.Null);
        if (okResult?.Value is DeviceStatusItem item)
        {
            Assert.That(item.DeviceId, Is.EqualTo(1));
            Assert.That(item.IsOnline, Is.True);
        }
    }

    [Test]
    public async Task Test_ReturnsNotFound_WhenDeviceMissing()
    {
        _monitoringServiceMock.Setup(s => s.Test(99, It.IsAny<CancellationToken>())).ReturnsAsync((DeviceStatusSnapshot?)null);
        var result = await _controller.Test(99);
        Assert.That(result.Result, Is.TypeOf<NotFoundObjectResult>());
    }

    [Test]
    public async Task Stream_SetsCorrectHeaders()
    {
        // Arrange
        var events = CreateEmptyAsyncEnumerable();
        _monitoringServiceMock.Setup(s => s.Subscribe(It.IsAny<CancellationToken>()))
            .Returns(events);

        using var cts = new CancellationTokenSource();
        cts.Cancel(); // Cancel immediately to prevent hanging

        // Act
        await _controller.Stream(cts.Token);

        // Assert
        Assert.That(_httpContext.Response.ContentType, Is.EqualTo("text/event-stream"));
        Assert.That(_httpContext.Response.Headers["Cache-Control"].ToString(), Is.EqualTo("no-cache"));
        Assert.That(_httpContext.Response.Headers["Connection"].ToString(), Is.EqualTo("keep-alive"));
    }

    [Test]
    public async Task Stream_WritesCorrectEventFormat()
    {
        // Arrange
        var snapshot = new DeviceStatusSnapshot
        {
            IpAddress = "192.168.1.10",
            IsOnline = true,
            LastChecked = DateTime.UtcNow,
            ConnectLatencyMs = 10,
            TotalLatencyMs = 20
        };
        var events = CreateSingleEventAsyncEnumerable(new DeviceStatusEvent(1, snapshot));

        _monitoringServiceMock.Setup(s => s.Subscribe(It.IsAny<CancellationToken>()))
            .Returns(events);

        using var cts = new CancellationTokenSource();

        // Act
        await _controller.Stream(cts.Token);

        // Assert
        _httpContext.Response.Body.Position = 0;
        var reader = new StreamReader(_httpContext.Response.Body);
        var content = await reader.ReadToEndAsync();
        
        Assert.That(content, Does.StartWith("data: "));
        Assert.That(content, Does.EndWith("\n\n"));
        
        // Extract JSON content and verify it contains expected values
        var jsonContent = content.Substring(6, content.Length - 8); // Remove "data: " and "\n\n"
        Assert.That(jsonContent, Does.Contain("\"deviceId\":1"));
        Assert.That(jsonContent, Does.Contain("\"isOnline\":true"));
        Assert.That(jsonContent, Does.Contain("\"connectLatencyMs\":10"));
        Assert.That(jsonContent, Does.Contain("\"totalLatencyMs\":20"));
    }

    [Test]
    public async Task Stream_HandlesMultipleEvents()
    {
        // Arrange
        var events = CreateMultipleEventsAsyncEnumerable();

        _monitoringServiceMock.Setup(s => s.Subscribe(It.IsAny<CancellationToken>()))
            .Returns(events);

        using var cts = new CancellationTokenSource();

        // Act
        await _controller.Stream(cts.Token);

        // Assert
        _httpContext.Response.Body.Position = 0;
        var reader = new StreamReader(_httpContext.Response.Body);
        var content = await reader.ReadToEndAsync();
        
        var eventStrings = content.Split(new[] { "\n\n" }, StringSplitOptions.RemoveEmptyEntries);
        Assert.That(eventStrings.Length, Is.EqualTo(2));
        
        // Verify first event contains expected values
        var firstJson = eventStrings[0].Substring(6); // Remove "data: "
        Assert.That(firstJson, Does.Contain("\"deviceId\":1"));
        
        // Verify second event contains expected values
        var secondJson = eventStrings[1].Substring(6); // Remove "data: "
        Assert.That(secondJson, Does.Contain("\"deviceId\":2"));
    }

    [Test]
    public async Task Stream_HandlesCancellation()
    {
        // Arrange
        var events = CreateInfiniteAsyncEnumerable();
        
        _monitoringServiceMock.Setup(s => s.Subscribe(It.IsAny<CancellationToken>()))
            .Returns(events);

        using var cts = new CancellationTokenSource();

        // Act
        var streamTask = _controller.Stream(cts.Token);
        
        // Cancel after a short delay
        await Task.Delay(100);
        cts.Cancel();
        
        // Should not throw
        await streamTask;

        // Assert - no exception should be thrown
        Assert.Pass("Stream handled cancellation gracefully");
    }

    [Test]
    public async Task Stream_HandlesEmptyEventStream()
    {
        // Arrange
        var events = CreateEmptyAsyncEnumerable();
        _monitoringServiceMock.Setup(s => s.Subscribe(It.IsAny<CancellationToken>()))
            .Returns(events);

        using var cts = new CancellationTokenSource();

        // Act
        await _controller.Stream(cts.Token);

        // Assert
        _httpContext.Response.Body.Position = 0;
        var reader = new StreamReader(_httpContext.Response.Body);
        var content = await reader.ReadToEndAsync();
        
        Assert.That(content, Is.Empty);
    }

    [Test]
    public async Task Stream_HandlesSubscribeException()
    {
        // Arrange
        _monitoringServiceMock.Setup(s => s.Subscribe(It.IsAny<CancellationToken>()))
            .Throws(new InvalidOperationException("Service unavailable"));

        using var cts = new CancellationTokenSource();
            
        // Act & Assert
        await Task.Delay(0);
        Assert.ThrowsAsync<InvalidOperationException>(async () => await _controller.Stream(cts.Token));
    }

    // Helper methods to create async enumerables
    private static async IAsyncEnumerable<DeviceStatusEvent> CreateEmptyAsyncEnumerable()
    {
        await Task.CompletedTask;
        yield break;
    }

    private static async IAsyncEnumerable<DeviceStatusEvent> CreateSingleEventAsyncEnumerable(DeviceStatusEvent eventItem)
    {
        await Task.Delay(1);
        yield return eventItem;
    }

    private static async IAsyncEnumerable<DeviceStatusEvent> CreateMultipleEventsAsyncEnumerable()
    {
        await Task.Delay(1);
        yield return new DeviceStatusEvent(1, new DeviceStatusSnapshot { IpAddress = "192.168.1.10", IsOnline = true, LastChecked = DateTime.UtcNow, ConnectLatencyMs = 10, TotalLatencyMs = 20 });
        yield return new DeviceStatusEvent(2, new DeviceStatusSnapshot { IpAddress = "192.168.1.11", IsOnline = false, LastChecked = DateTime.UtcNow, ConnectLatencyMs = 30, TotalLatencyMs = 40 });
    }

    private static async IAsyncEnumerable<DeviceStatusEvent> CreateInfiniteAsyncEnumerable([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var snapshot = new DeviceStatusSnapshot
        {
            IpAddress = "192.168.1.10",
            IsOnline = true,
            LastChecked = DateTime.UtcNow,
            ConnectLatencyMs = 10,
            TotalLatencyMs = 20
        };

        int deviceId = 1;
        while (!cancellationToken.IsCancellationRequested)
        {
            yield return new DeviceStatusEvent(deviceId++, snapshot);
            try
            {
                await Task.Delay(50, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                yield break;
            }
            
            if (deviceId > 100) // Safety net
                yield break;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediaPi.Core.Controllers;
using MediaPi.Core.RestModels;
using MediaPi.Core.Services;
using MediaPi.Core.Services.Models;
using Microsoft.AspNetCore.Mvc;
using Moq;
using NUnit.Framework;

namespace MediaPi.Core.Tests.Controllers;

[TestFixture]
public class DeviceStatusControllerTests
{
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor.
    private Mock<IDeviceMonitoringService> _monitoringServiceMock;
    private DeviceStatusController _controller;
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor.

    [SetUp]
    public void SetUp()
    {
        _monitoringServiceMock = new Mock<IDeviceMonitoringService>();
        _controller = new DeviceStatusController(_monitoringServiceMock.Object);
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
    public void Get_ReturnsDeviceStatus_WhenFound()
    {
        var snapshot = new DeviceStatusSnapshot
        {
            IpAddress = "192.168.1.10",
            IsOnline = true,
            LastChecked = DateTime.UtcNow,
            ConnectLatencyMs = 10,
            TotalLatencyMs = 20
        };
        _monitoringServiceMock.Setup(s => s.TryGetStatus(1, out snapshot)).Returns(true);
        var result = _controller.Get(1);
        Assert.That(result.Result, Is.TypeOf<OkObjectResult>());
        var okResult = result.Result as OkObjectResult;
        Assert.That(okResult?.Value, Is.Not.Null);
        if (okResult?.Value is DeviceStatusItem item)
        {
            Assert.That(item.DeviceId, Is.EqualTo(1));
            Assert.That(item.IpAddress, Is.EqualTo("192.168.1.10"));
            Assert.That(item.IsOnline, Is.True);
        }
    }

    [Test]
    public void Get_ReturnsNotFound_WhenDeviceMissing()
    {
        DeviceStatusSnapshot dummy;
        _monitoringServiceMock.Setup(s => s.TryGetStatus(99, out dummy)).Returns(false);
        var result = _controller.Get(99);
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
            Assert.That(item.IpAddress, Is.EqualTo("192.168.1.10"));
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
}

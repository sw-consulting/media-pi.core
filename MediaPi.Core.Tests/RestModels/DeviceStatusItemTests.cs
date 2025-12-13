// Copyright (c) 2025 sw.consulting
// This file is a part of Media Pi backend

using System;
using System.Text.Json;
using MediaPi.Core.RestModels;
using MediaPi.Core.Services.Models;
using NUnit.Framework;

namespace MediaPi.Core.Tests.RestModels;

[TestFixture]
public class DeviceStatusItemTests
{
    [Test]
    public void Constructor_CopiesAllPropertiesFromSnapshot()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var snapshot = new DeviceStatusSnapshot
        {
            IpAddress = "192.168.1.100",
            IsOnline = true,
            LastChecked = now,
            ConnectLatencyMs = 150,
            TotalLatencyMs = 200,
            SoftwareVersion = "5.2.1"
        };

        // Act
        var statusItem = new DeviceStatusItem(42, snapshot);

        // Assert
        Assert.That(statusItem.DeviceId, Is.EqualTo(42));
        Assert.That(statusItem.IsOnline, Is.EqualTo(true));
        Assert.That(statusItem.LastChecked, Is.EqualTo(now));
        Assert.That(statusItem.ConnectLatencyMs, Is.EqualTo(150));
        Assert.That(statusItem.TotalLatencyMs, Is.EqualTo(200));
        Assert.That(statusItem.SoftwareVersion, Is.EqualTo("5.2.1"));
    }

    [Test]
    public void Constructor_HandlesNullSoftwareVersion()
    {
        // Arrange
        var snapshot = new DeviceStatusSnapshot
        {
            IpAddress = "192.168.1.100",
            IsOnline = false,
            LastChecked = DateTime.UtcNow,
            ConnectLatencyMs = 0,
            TotalLatencyMs = 500,
            SoftwareVersion = null
        };

        // Act
        var statusItem = new DeviceStatusItem(99, snapshot);

        // Assert
        Assert.That(statusItem.DeviceId, Is.EqualTo(99));
        Assert.That(statusItem.IsOnline, Is.False);
        Assert.That(statusItem.SoftwareVersion, Is.Null);
    }

    [Test]
    public void Constructor_HandlesOfflineDevice()
    {
        // Arrange
        var snapshot = new DeviceStatusSnapshot
        {
            IpAddress = "192.168.1.200",
            IsOnline = false,
            LastChecked = DateTime.UtcNow.AddMinutes(-5),
            ConnectLatencyMs = 1000,
            TotalLatencyMs = 1000,
            SoftwareVersion = null
        };

        // Act
        var statusItem = new DeviceStatusItem(7, snapshot);

        // Assert
        Assert.That(statusItem.DeviceId, Is.EqualTo(7));
        Assert.That(statusItem.IsOnline, Is.False);
        Assert.That(statusItem.ConnectLatencyMs, Is.EqualTo(1000));
        Assert.That(statusItem.TotalLatencyMs, Is.EqualTo(1000));
        Assert.That(statusItem.SoftwareVersion, Is.Null);
    }

    [Test]
    public void ToString_ReturnsValidJson()
    {
        // Arrange
        var snapshot = new DeviceStatusSnapshot
        {
            IpAddress = "10.0.0.1",
            IsOnline = true,
            LastChecked = new DateTime(2024, 1, 15, 10, 30, 45, DateTimeKind.Utc),
            ConnectLatencyMs = 25,
            TotalLatencyMs = 50,
            SoftwareVersion = "6.0.0-beta"
        };
        var statusItem = new DeviceStatusItem(123, snapshot);

        // Act
        var jsonString = statusItem.ToString();

        // Assert
        Assert.That(jsonString, Is.Not.Null.And.Not.Empty);
        
        // Verify it's valid JSON by deserializing it back
        var deserializedItem = JsonSerializer.Deserialize<DeviceStatusItem>(jsonString);
        Assert.That(deserializedItem, Is.Not.Null);
        Assert.That(deserializedItem!.DeviceId, Is.EqualTo(123));
        Assert.That(deserializedItem.IsOnline, Is.True);
        Assert.That(deserializedItem.ConnectLatencyMs, Is.EqualTo(25));
        Assert.That(deserializedItem.TotalLatencyMs, Is.EqualTo(50));
        Assert.That(deserializedItem.SoftwareVersion, Is.EqualTo("6.0.0-beta"));
    }

    [Test]
    public void ToString_HandlesNullSoftwareVersionInJson()
    {
        // Arrange
        var snapshot = new DeviceStatusSnapshot
        {
            IpAddress = "172.16.0.1",
            IsOnline = false,
            LastChecked = DateTime.UtcNow,
            ConnectLatencyMs = 0,
            TotalLatencyMs = 0,
            SoftwareVersion = null
        };
        var statusItem = new DeviceStatusItem(456, snapshot);

        // Act
        var jsonString = statusItem.ToString();

        // Assert
        Assert.That(jsonString, Is.Not.Null.And.Not.Empty);
        Assert.That(jsonString, Does.Contain("\"SoftwareVersion\": null"));
        
        // Verify it's valid JSON by deserializing it back
        var deserializedItem = JsonSerializer.Deserialize<DeviceStatusItem>(jsonString);
        Assert.That(deserializedItem, Is.Not.Null);
        Assert.That(deserializedItem!.DeviceId, Is.EqualTo(456));
        Assert.That(deserializedItem.SoftwareVersion, Is.Null);
    }

    [Test]
    public void Constructor_WithZeroLatencies()
    {
        // Arrange
        var snapshot = new DeviceStatusSnapshot
        {
            IpAddress = "127.0.0.1",
            IsOnline = true,
            LastChecked = DateTime.UtcNow,
            ConnectLatencyMs = 0,
            TotalLatencyMs = 0,
            SoftwareVersion = "1.0.0"
        };

        // Act
        var statusItem = new DeviceStatusItem(1, snapshot);

        // Assert
        Assert.That(statusItem.ConnectLatencyMs, Is.EqualTo(0));
        Assert.That(statusItem.TotalLatencyMs, Is.EqualTo(0));
        Assert.That(statusItem.SoftwareVersion, Is.EqualTo("1.0.0"));
    }

    [Test]
    public void Constructor_WithHighLatencies()
    {
        // Arrange
        var snapshot = new DeviceStatusSnapshot
        {
            IpAddress = "8.8.8.8",
            IsOnline = true,
            LastChecked = DateTime.UtcNow,
            ConnectLatencyMs = 5000,
            TotalLatencyMs = 10000,
            SoftwareVersion = "0.9.9"
        };

        // Act
        var statusItem = new DeviceStatusItem(999, snapshot);

        // Assert
        Assert.That(statusItem.ConnectLatencyMs, Is.EqualTo(5000));
        Assert.That(statusItem.TotalLatencyMs, Is.EqualTo(10000));
        Assert.That(statusItem.SoftwareVersion, Is.EqualTo("0.9.9"));
    }
}
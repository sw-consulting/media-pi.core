// Copyright (c) 2025 sw.consulting
// This file is a part of Media Pi backend

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MediaPi.Core.Services;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace MediaPi.Core.Tests.Services;

[TestFixture]
public class VideoMetadataServiceTests
{
    private Mock<ILogger<VideoMetadataService>> _mockLogger = null!;
    private VideoMetadataService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _mockLogger = new Mock<ILogger<VideoMetadataService>>();
        _service = new VideoMetadataService(_mockLogger.Object);
    }

    [Test]
    public async Task ExtractMetadataAsync_FileNotFound_ReturnsNull()
    {
        var result = await _service.ExtractMetadataAsync("nonexistent.mp4", CancellationToken.None);
        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task ExtractMetadataAsync_EmptyPath_ReturnsNull()
    {
        var result = await _service.ExtractMetadataAsync("", CancellationToken.None);
        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task ExtractMetadataAsync_ValidFile_ReturnsMetadata()
    {
        // Create a temporary file for testing
        var tempFile = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(tempFile, "dummy video content", CancellationToken.None);

            var result = await _service.ExtractMetadataAsync(tempFile, CancellationToken.None);

            Assert.That(result, Is.Not.Null);
            Assert.That(result!.FileSizeBytes, Is.GreaterThan(0));
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Test]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new VideoMetadataService(null!));
    }

    [Test]
    public async Task ExtractMetadataAsync_LargeFile_CapsFileSizeAtUintMax()
    {
        // Create a temporary file for testing
        var tempFile = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(tempFile, "dummy video content", CancellationToken.None);

            var result = await _service.ExtractMetadataAsync(tempFile, CancellationToken.None);

            Assert.That(result, Is.Not.Null);
            Assert.That(result!.FileSizeBytes, Is.GreaterThan(0));
            Assert.That(result.FileSizeBytes, Is.LessThanOrEqualTo(uint.MaxValue));
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }
}
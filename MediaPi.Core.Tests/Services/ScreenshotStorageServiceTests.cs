// Copyright (c) 2025-2026 sw.consulting
// This file is a part of Media Pi backend

using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MediaPi.Core.Services;
using MediaPi.Core.Settings;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Moq;
using NUnit.Framework;

namespace MediaPi.Core.Tests.Services;

[TestFixture]
public class ScreenshotStorageServiceTests
{
    private Mock<IOptions<VideoStorageSettings>> _mockOptions = null!;
    private VideoStorageSettings _settings = null!;
    private string _testRootPath = null!;
    private ScreenshotStorageService _service = null!;
    private readonly ConcurrentBag<MemoryStream> _memoryStreams = new();

    [SetUp]
    public void SetUp()
    {
        _testRootPath = Path.Combine(Path.GetTempPath(), $"screenshot_storage_test_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testRootPath);

        _settings = new VideoStorageSettings
        {
            RootPath = _testRootPath,
            MaxFilesPerDirectory = 2
        };

        _mockOptions = new Mock<IOptions<VideoStorageSettings>>();
        _mockOptions.Setup(x => x.Value).Returns(_settings);
        _service = new ScreenshotStorageService(_mockOptions.Object);
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var stream in _memoryStreams)
        {
            stream.Dispose();
        }

        _memoryStreams.Clear();

        if (Directory.Exists(_testRootPath))
        {
            Directory.Delete(_testRootPath, true);
        }
    }

    private Mock<IFormFile> CreateMockFormFile(string fileName, string content)
    {
        var mockFile = new Mock<IFormFile>();
        var ms = new MemoryStream();
        _memoryStreams.Add(ms);
        using (var writer = new StreamWriter(ms, leaveOpen: true))
        {
            writer.Write(content);
            writer.Flush();
        }

        ms.Position = 0;
        mockFile.Setup(f => f.FileName).Returns(fileName);
        mockFile.Setup(f => f.Length).Returns(ms.Length);
        mockFile.Setup(f => f.CopyToAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .Returns((Stream stream, CancellationToken token) => ms.CopyToAsync(stream, token));

        return mockFile;
    }

    [Test]
    public async Task SaveScreenshotAsync_WithMatchingCameraName_ExtractsTimeCreatedFromFilename()
    {
        var file = CreateMockFormFile("/home/pi/Pictures/cam_2026-03-11_15-23-59.jpg", "image-content");

        var result = await _service.SaveScreenshotAsync(file.Object, "Cam Shot");

        Assert.That(result.TimeCreated, Is.EqualTo(new DateTime(2026, 3, 11, 15, 23, 59, DateTimeKind.Utc)));
        Assert.That(result.Sha256, Is.Null);
    }

    [Test]
    public async Task SaveScreenshotAsync_WithMatchingCameraBasename_ExtractsTimeCreatedFromFilename()
    {
        var file = CreateMockFormFile("cam_2026-03-11_15-23-59.jpg", "image-content");

        var result = await _service.SaveScreenshotAsync(file.Object, "Cam Shot");

        Assert.That(result.TimeCreated, Is.EqualTo(new DateTime(2026, 3, 11, 15, 23, 59, DateTimeKind.Utc)));
        Assert.That(result.Sha256, Is.Null);
    }

    [Test]
    public async Task SaveScreenshotAsync_WithNonMatchingName_UsesCurrentUtcTime()
    {
        var file = CreateMockFormFile("camera-shot.jpg", "image-content");
        var before = DateTime.UtcNow;

        var result = await _service.SaveScreenshotAsync(file.Object, "Cam Shot");

        var after = DateTime.UtcNow;
        Assert.That(result.TimeCreated, Is.GreaterThanOrEqualTo(before).And.LessThanOrEqualTo(after));
    }

    [Test]
    public async Task SaveScreenshotAsync_WithInvalidTimestamp_UsesCurrentUtcTime()
    {
        var file = CreateMockFormFile("cam_2026-13-99_99-99-99.jpg", "image-content");
        var before = DateTime.UtcNow;

        var result = await _service.SaveScreenshotAsync(file.Object, "Cam Shot");

        var after = DateTime.UtcNow;
        Assert.That(result.TimeCreated, Is.GreaterThanOrEqualTo(before).And.LessThanOrEqualTo(after));
    }

    [Test]
    public async Task SaveScreenshotAsync_EmptyTitle_UsesScreenshotFallbackToken()
    {
        var file = CreateMockFormFile("shot.jpg", "image-content");

        var result = await _service.SaveScreenshotAsync(file.Object, string.Empty);

        Assert.That(result.Filename, Does.Contain("screenshot"));
    }

    [Test]
    public async Task DeleteScreenshotAsync_ExistingFile_DeletesStoredFile()
    {
        var file = CreateMockFormFile("shot.jpg", "image-content");
        var result = await _service.SaveScreenshotAsync(file.Object, "Cam Shot");

        await _service.DeleteScreenshotAsync(result.Filename);

        Assert.That(File.Exists(_service.GetAbsolutePath(result.Filename)), Is.False);
    }

    [Test]
    public async Task SaveScreenshotAsync_ReturnsFileStorageFields()
    {
        var file = CreateMockFormFile("shot.jpg", "image-content");

        var result = await _service.SaveScreenshotAsync(file.Object, "Title");

        Assert.Multiple(() =>
        {
            Assert.That(result.OriginalFilename, Is.EqualTo("shot.jpg"));
            Assert.That(result.FileSizeBytes, Is.EqualTo((uint)"image-content".Length));
            Assert.That(result.Filename, Does.EndWith(".jpg"));
        });
    }
}

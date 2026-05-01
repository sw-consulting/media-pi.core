// Copyright (C) 2025-2026 sw.consulting
// This file is a part of Media Pi backend

using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MediaPi.Core.Services;
using Microsoft.AspNetCore.Http;
using Moq;
using NUnit.Framework;

namespace MediaPi.Core.Tests.Services;

[TestFixture]
public class FileStorageServiceTests
{
    private const int MaxFilesPerDirectory = 2;

    private string _testRootPath = null!;
    private FileStorageService _service = null!;
    private readonly ConcurrentBag<MemoryStream> _memoryStreams = new();

    [SetUp]
    public void SetUp()
    {
        _testRootPath = Path.Combine(Path.GetTempPath(), $"file_storage_test_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testRootPath);
        _service = new FileStorageService(_testRootPath, MaxFilesPerDirectory);
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
    public async Task SaveFileAsync_ValidFile_SavesWithoutVideoMetadata()
    {
        var mockFile = CreateMockFormFile("poster.png", "image-content");

        var result = await _service.SaveFileAsync(mockFile.Object, "Poster Image");

        Assert.That(result.OriginalFilename, Is.EqualTo("poster.png"));
        Assert.That(result.Filename, Does.Contain("poster-image"));
        Assert.That(result.Filename, Does.EndWith(".png"));
        var fullPath = _service.GetAbsolutePath(result.Filename);
        Assert.That(File.Exists(fullPath), Is.True);
    }

    [Test]
    public async Task SaveFileAsync_EmptyTitle_UsesFileFallback()
    {
        var mockFile = CreateMockFormFile("asset", "abc");

        var result = await _service.SaveFileAsync(mockFile.Object, string.Empty);

        Assert.That(result.Filename, Does.Contain("file"));
        Assert.That(result.Filename, Does.EndWith(".bin"));
    }

    [Test]
    public async Task SaveFileAsync_RespectsMaxFilesPerDirectory()
    {
        for (int i = 0; i < MaxFilesPerDirectory + 1; i++)
        {
            var mockFile = CreateMockFormFile($"image{i}.jpg", $"content{i}");
            await _service.SaveFileAsync(mockFile.Object, $"Image {i}");
        }

        var directories = Directory.GetDirectories(_testRootPath);
        Assert.That(directories.Length, Is.EqualTo(2));
    }

    [Test]
    public async Task DeleteFileAsync_ExistingFile_DeletesSuccessfully()
    {
        var mockFile = CreateMockFormFile("to-delete.png", "content");
        var result = await _service.SaveFileAsync(mockFile.Object, "Delete me");

        await _service.DeleteFileAsync(result.Filename);

        Assert.That(File.Exists(_service.GetAbsolutePath(result.Filename)), Is.False);
    }

    [Test]
    public void GetAbsolutePath_PathTraversal_ThrowsInvalidOperationException()
    {
        Assert.Throws<InvalidOperationException>(() => _service.GetAbsolutePath("../../../etc/passwd"));
    }

    [Test]
    public async Task SaveFileAsync_WithComputeSha256_ReturnsCorrectHash()
    {
        var content = "hello world";
        var mockFile = CreateMockFormFile("image.png", content);

        var result = await _service.SaveFileAsync(mockFile.Object, "Image", computeSha256: true);

        Assert.That(result.Sha256, Is.Not.Null.And.Not.Empty);

        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var bytes = System.Text.Encoding.UTF8.GetBytes(content);
        var expectedHash = BitConverter.ToString(sha256.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        Assert.That(result.Sha256, Is.EqualTo(expectedHash));
    }

    [Test]
    public async Task SaveFileAsync_WithoutComputeSha256_ReturnsNullHash()
    {
        var mockFile = CreateMockFormFile("image.png", "content");

        var result = await _service.SaveFileAsync(mockFile.Object, "Image");

        Assert.That(result.Sha256, Is.Null);
    }
}

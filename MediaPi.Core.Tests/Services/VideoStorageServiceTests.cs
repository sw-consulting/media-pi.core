// Copyright (c) 2025 sw.consulting
// This file is a part of Media Pi backend

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediaPi.Core.Services;
using MediaPi.Core.Services.Interfaces;
using MediaPi.Core.Settings;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Moq;
using NUnit.Framework;

namespace MediaPi.Core.Tests.Services;

[TestFixture]
public class VideoStorageServiceTests
{
    private Mock<IOptions<VideoStorageSettings>> _mockOptions = null!;
    private Mock<IVideoMetadataService> _mockMetadataService = null!;
    private VideoStorageSettings _settings = null!;
    private string _testRootPath = null!;
    private VideoStorageService _service = null!;

    [SetUp]
    public void SetUp()
    {
        // Create a unique temp directory for each test
        _testRootPath = Path.Combine(Path.GetTempPath(), $"video_storage_test_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testRootPath);

        _settings = new VideoStorageSettings
        {
            RootPath = _testRootPath,
            MaxFilesPerDirectory = 3
        };

        _mockOptions = new Mock<IOptions<VideoStorageSettings>>();
        _mockOptions.Setup(x => x.Value).Returns(_settings);

        _mockMetadataService = new Mock<IVideoMetadataService>();
        
        // Default metadata service behavior
        _mockMetadataService
            .Setup(x => x.ExtractMetadataAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VideoMetadata
            {
                FileSizeBytes = 1000,
                DurationSeconds = 60,
                Format = "MP4",
                Width = 1920,
                Height = 1080
            });

        _service = new VideoStorageService(_mockOptions.Object, _mockMetadataService.Object);
    }

    [TearDown]
    public void TearDown()
    {
        // Clean up test directory
        if (Directory.Exists(_testRootPath))
        {
            Directory.Delete(_testRootPath, true);
        }
    }

    private Mock<IFormFile> CreateMockFormFile(string fileName, string content)
    {
        var mockFile = new Mock<IFormFile>();
        var ms = new MemoryStream();
        using (var writer = new StreamWriter(ms))
        {
            writer.Write(content);
            writer.Flush();
        }
        ms.Position = 0;

        mockFile.Setup(f => f.FileName).Returns(fileName);
        mockFile.Setup(f => f.Length).Returns(ms.Length);
        mockFile.Setup(f => f.OpenReadStream()).Returns(ms);
        mockFile.Setup(f => f.CopyToAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .Returns((Stream stream, CancellationToken token) => ms.CopyToAsync(stream, token));

        return mockFile;
    }

    [Test]
    public void Constructor_NullOptions_ThrowsException()
    {
        // VideoStorageService will throw NullReferenceException when accessing options.Value
        Assert.Throws<NullReferenceException>(() =>
            new VideoStorageService(null!, _mockMetadataService.Object));
    }

    [Test]
    public void Constructor_NullMetadataService_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new VideoStorageService(_mockOptions.Object, null!));
    }

    [Test]
    public void Constructor_NullRootPath_ThrowsArgumentException()
    {
        var settings = new VideoStorageSettings { RootPath = null! };
        var mockOptions = new Mock<IOptions<VideoStorageSettings>>();
        mockOptions.Setup(x => x.Value).Returns(settings);

        Assert.Throws<ArgumentException>(() =>
            new VideoStorageService(mockOptions.Object, _mockMetadataService.Object));
    }

    [Test]
    public void Constructor_EmptyRootPath_ThrowsArgumentException()
    {
        var settings = new VideoStorageSettings { RootPath = "" };
        var mockOptions = new Mock<IOptions<VideoStorageSettings>>();
        mockOptions.Setup(x => x.Value).Returns(settings);

        Assert.Throws<ArgumentException>(() =>
            new VideoStorageService(mockOptions.Object, _mockMetadataService.Object));
    }

    [Test]
    public void Constructor_CreatesRootDirectory()
    {
        var newRootPath = Path.Combine(Path.GetTempPath(), $"video_storage_new_{Guid.NewGuid()}");
        try
        {
            var settings = new VideoStorageSettings { RootPath = newRootPath };
            var mockOptions = new Mock<IOptions<VideoStorageSettings>>();
            mockOptions.Setup(x => x.Value).Returns(settings);

            var service = new VideoStorageService(mockOptions.Object, _mockMetadataService.Object);

            Assert.That(Directory.Exists(newRootPath), Is.True);
        }
        finally
        {
            if (Directory.Exists(newRootPath))
            {
                Directory.Delete(newRootPath, true);
            }
        }
    }

    [Test]
    public async Task SaveVideoAsync_NullFile_ThrowsArgumentNullException()
    {
        Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await _service.SaveVideoAsync(null!, "title"));
    }

    [Test]
    public async Task SaveVideoAsync_EmptyFile_ThrowsArgumentException()
    {
        var mockFile = CreateMockFormFile("video.mp4", "");
        mockFile.Setup(f => f.Length).Returns(0);

        Assert.ThrowsAsync<ArgumentException>(async () =>
            await _service.SaveVideoAsync(mockFile.Object, "title"));
    }

    [Test]
    public async Task SaveVideoAsync_ValidFile_SavesSuccessfully()
    {
        var mockFile = CreateMockFormFile("test-video.mp4", "video content");
        
        var result = await _service.SaveVideoAsync(mockFile.Object, "Test Video");

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Filename, Does.Contain("test-video"));
        Assert.That(result.Filename, Does.EndWith(".mp4"));
        Assert.That(result.OriginalFilename, Is.EqualTo("test-video.mp4"));
        Assert.That(result.FileSizeBytes, Is.GreaterThan(0));
    }

    [Test]
    public async Task SaveVideoAsync_CreatesSubdirectory()
    {
        var mockFile = CreateMockFormFile("video.mp4", "content");
        
        var result = await _service.SaveVideoAsync(mockFile.Object, "Video");

        var fullPath = _service.GetAbsolutePath(result.Filename);
        Assert.That(File.Exists(fullPath), Is.True);
        
        // Check that file is in a subdirectory
        var relativePath = Path.GetDirectoryName(result.Filename);
        Assert.That(relativePath, Is.Not.Null.And.Not.Empty);
    }

    [Test]
    public async Task SaveVideoAsync_RespectsMaxFilesPerDirectory()
    {
        // Save files up to the limit
        for (int i = 0; i < _settings.MaxFilesPerDirectory; i++)
        {
            var mockFile = CreateMockFormFile($"video{i}.mp4", $"content{i}");
            await _service.SaveVideoAsync(mockFile.Object, $"Video {i}");
        }

        // Check that only one directory exists
        var directories = Directory.GetDirectories(_testRootPath);
        Assert.That(directories.Length, Is.EqualTo(1));

        // Save one more file - should create a new directory
        var extraFile = CreateMockFormFile("extra.mp4", "extra content");
        await _service.SaveVideoAsync(extraFile.Object, "Extra Video");

        directories = Directory.GetDirectories(_testRootPath);
        Assert.That(directories.Length, Is.EqualTo(2));
    }

    [Test]
    public async Task SaveVideoAsync_SanitizesTitle()
    {
        var mockFile = CreateMockFormFile("video.mp4", "content");
        
        // Use characters that are actually invalid on the current platform (excluding null and path separators)
        var invalidChars = Path.GetInvalidFileNameChars()
            .Where(c => c != '\0' && c != '/' && c != '\\')
            .Take(3)
            .ToArray();
        var testTitle = "Test/Video" + new string(invalidChars) + "Name";
        
        var result = await _service.SaveVideoAsync(mockFile.Object, testTitle);

        // Extract just the filename part (not the directory)
        var actualFileName = Path.GetFileName(result.Filename);
        
        // Verify that invalid characters are replaced with dashes in the filename
        Assert.That(actualFileName, Does.Contain("test-video"));
        // Verify no invalid filename characters remain in the actual filename part
        foreach (var invalidChar in invalidChars)
        {
            Assert.That(actualFileName, Does.Not.Contain(invalidChar.ToString()));
        }
    }

    [Test]
    public async Task SaveVideoAsync_EmptyTitle_UsesFallback()
    {
        var mockFile = CreateMockFormFile("test.mp4", "content");
        
        var result = await _service.SaveVideoAsync(mockFile.Object, "");

        Assert.That(result.Filename, Does.Contain("video"));
    }

    [Test]
    public async Task SaveVideoAsync_TitleWithOnlyInvalidChars_UsesFallback()
    {
        var mockFile = CreateMockFormFile("test.mp4", "content");
        
        // Use only characters that are invalid on the current platform
        var invalidChars = Path.GetInvalidFileNameChars();
        var testTitle = new string(invalidChars.Take(5).ToArray());
        
        var result = await _service.SaveVideoAsync(mockFile.Object, testTitle);

        // When all characters are invalid, should fall back to "video"
        Assert.That(result.Filename, Does.Contain("video"));
    }

    [Test]
    public async Task SaveVideoAsync_NoFileExtension_UsesDefaultExtension()
    {
        var mockFile = CreateMockFormFile("videofile", "content");
        
        var result = await _service.SaveVideoAsync(mockFile.Object, "Title");

        Assert.That(result.Filename, Does.EndWith(".bin"));
    }

    [Test]
    public async Task SaveVideoAsync_PreservesFileExtension()
    {
        var mockFile = CreateMockFormFile("video.avi", "content");
        
        var result = await _service.SaveVideoAsync(mockFile.Object, "Title");

        Assert.That(result.Filename, Does.EndWith(".avi"));
    }

    [Test]
    public async Task SaveVideoAsync_CallsMetadataService()
    {
        var mockFile = CreateMockFormFile("video.mp4", "content");
        
        await _service.SaveVideoAsync(mockFile.Object, "Title");

        _mockMetadataService.Verify(
            x => x.ExtractMetadataAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Test]
    public async Task SaveVideoAsync_UsesMetadataFromService()
    {
        var expectedMetadata = new VideoMetadata
        {
            FileSizeBytes = 5000,
            DurationSeconds = 120,
            Format = "AVI",
            Width = 1280,
            Height = 720
        };

        _mockMetadataService
            .Setup(x => x.ExtractMetadataAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedMetadata);

        var mockFile = CreateMockFormFile("video.avi", "content");
        
        var result = await _service.SaveVideoAsync(mockFile.Object, "Title");

        Assert.That(result.FileSizeBytes, Is.EqualTo(expectedMetadata.FileSizeBytes));
        Assert.That(result.DurationSeconds, Is.EqualTo(expectedMetadata.DurationSeconds));
        Assert.That(result.Format, Is.EqualTo(expectedMetadata.Format));
        Assert.That(result.Width, Is.EqualTo(expectedMetadata.Width));
        Assert.That(result.Height, Is.EqualTo(expectedMetadata.Height));
    }

    [Test]
    public async Task SaveVideoAsync_MetadataServiceReturnsNull_UsesFallbackFileSize()
    {
        _mockMetadataService
            .Setup(x => x.ExtractMetadataAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((VideoMetadata?)null);

        var mockFile = CreateMockFormFile("video.mp4", "test content");
        
        var result = await _service.SaveVideoAsync(mockFile.Object, "Title");

        Assert.That(result.FileSizeBytes, Is.EqualTo(mockFile.Object.Length));
        Assert.That(result.DurationSeconds, Is.Null);
    }

    [Test]
    public async Task SaveVideoAsync_LargeFile_ThrowsArgumentOutOfRangeException()
    {
        var mockFile = CreateMockFormFile("large.mp4", "content");
        mockFile.Setup(f => f.Length).Returns((long)uint.MaxValue + 1000);

        var ex = Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
            await _service.SaveVideoAsync(mockFile.Object, "Large Video"));

        Assert.That(ex.ParamName, Is.EqualTo("file"));
        Assert.That(ex.Message, Does.Contain("exceeds maximum supported size"));
        Assert.That(ex.Message, Does.Contain("4GB"));
    }

    [Test]
    public async Task SaveVideoAsync_UniqueFilenames_NoDuplicates()
    {
        var mockFile1 = CreateMockFormFile("video.mp4", "content1");
        var mockFile2 = CreateMockFormFile("video.mp4", "content2");

        var result1 = await _service.SaveVideoAsync(mockFile1.Object, "Same Title");
        var result2 = await _service.SaveVideoAsync(mockFile2.Object, "Same Title");

        Assert.That(result1.Filename, Is.Not.EqualTo(result2.Filename));
    }

    [Test]
    public async Task SaveVideoAsync_NormalizesRelativePath()
    {
        var mockFile = CreateMockFormFile("video.mp4", "content");
        
        var result = await _service.SaveVideoAsync(mockFile.Object, "Title");

        // Should use forward slashes, not backslashes
        Assert.That(result.Filename, Does.Not.Contain("\\"));
        if (result.Filename.Contains("/"))
        {
            Assert.That(result.Filename, Does.Match(".*/.*"));
        }
    }

    [Test]
    public async Task DeleteVideoAsync_ExistingFile_DeletesSuccessfully()
    {
        // First, save a video
        var mockFile = CreateMockFormFile("video.mp4", "content");
        var saveResult = await _service.SaveVideoAsync(mockFile.Object, "Test");

        var fullPath = _service.GetAbsolutePath(saveResult.Filename);
        Assert.That(File.Exists(fullPath), Is.True);

        // Now delete it
        await _service.DeleteVideoAsync(saveResult.Filename);

        Assert.That(File.Exists(fullPath), Is.False);
    }

    [Test]
    public async Task DeleteVideoAsync_NonExistentFile_DoesNotThrow()
    {
        Assert.DoesNotThrowAsync(async () =>
            await _service.DeleteVideoAsync("nonexistent/file.mp4"));
    }

    [Test]
    public void GetAbsolutePath_NullFilename_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            _service.GetAbsolutePath(null!));
    }

    [Test]
    public void GetAbsolutePath_EmptyFilename_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            _service.GetAbsolutePath(""));
    }

    [Test]
    public void GetAbsolutePath_WhitespaceFilename_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            _service.GetAbsolutePath("   "));
    }

    [Test]
    public void GetAbsolutePath_ValidFilename_ReturnsFullPath()
    {
        var filename = "0001/video.mp4";
        
        var fullPath = _service.GetAbsolutePath(filename);

        Assert.That(fullPath, Does.Contain(_testRootPath));
        Assert.That(fullPath, Does.EndWith("video.mp4"));
    }

    [Test]
    public void GetAbsolutePath_PathTraversal_ThrowsInvalidOperationException()
    {
        Assert.Throws<InvalidOperationException>(() =>
            _service.GetAbsolutePath("../../../etc/passwd"));
    }

    [Test]
    public void GetAbsolutePath_AbsolutePath_ThrowsInvalidOperationException()
    {
        var absolutePath = Path.Combine(Path.GetTempPath(), "video.mp4");
        
        Assert.Throws<InvalidOperationException>(() =>
            _service.GetAbsolutePath(absolutePath));
    }

    [Test]
    public void GetAbsolutePath_WindowsStylePath_HandledCorrectly()
    {
        // On Linux, backslashes are valid filename characters, so this might not throw
        // The test verifies that the method handles Windows-style paths
        var testPath = @"..\..\..\windows\system32\config\sam";
        
        try
        {
            var result = _service.GetAbsolutePath(testPath);
            // If it doesn't throw, verify it's still within the root
            Assert.That(result, Does.StartWith(_testRootPath));
        }
        catch (InvalidOperationException)
        {
            // Expected behavior if path traversal is detected
            Assert.Pass();
        }
    }

    [Test]
    public async Task SaveVideoAsync_WithCancellationToken_RespectsToken()
    {
        var mockFile = CreateMockFormFile("video.mp4", "content");
        using (var cts = new CancellationTokenSource())
        {
            cts.Cancel();

            Assert.ThrowsAsync<TaskCanceledException>(async () =>
                await _service.SaveVideoAsync(mockFile.Object, "Title", cts.Token));
        }
    }

    [Test]
    public async Task SaveVideoAsync_DirectoryNaming_IncrementalNumeric()
    {
        // Create multiple files to trigger directory creation
        for (int i = 0; i < _settings.MaxFilesPerDirectory + 1; i++)
        {
            var mockFile = CreateMockFormFile($"video{i}.mp4", $"content{i}");
            await _service.SaveVideoAsync(mockFile.Object, $"Video {i}");
        }

        var directories = Directory.GetDirectories(_testRootPath)
            .Select(Path.GetFileName)
            .OrderBy(x => x)
            .ToList();

        Assert.That(directories.Count, Is.GreaterThanOrEqualTo(2));
        // Check that directories are numerically named (e.g., 0001, 0002)
        Assert.That(directories[0], Does.Match(@"^\d{4}$"));
    }

    [Test]
    public async Task SaveVideoAsync_MultipleThreads_ThreadSafe()
    {
        var tasks = Enumerable.Range(0, 10).Select(async i =>
        {
            var mockFile = CreateMockFormFile($"video{i}.mp4", $"content{i}");
            return await _service.SaveVideoAsync(mockFile.Object, $"Video {i}");
        });

        var results = await Task.WhenAll(tasks);

        // All saves should succeed
        Assert.That(results.Length, Is.EqualTo(10));
        
        // All filenames should be unique
        var uniqueFilenames = results.Select(r => r.Filename).Distinct().Count();
        Assert.That(uniqueFilenames, Is.EqualTo(10));
    }

    [Test]
    public async Task SaveVideoAsync_TitleWithSpaces_ReplacesWithDashes()
    {
        var mockFile = CreateMockFormFile("video.mp4", "content");
        
        var result = await _service.SaveVideoAsync(mockFile.Object, "My Test Video");

        Assert.That(result.Filename, Does.Contain("my-test-video"));
        Assert.That(result.Filename, Does.Not.Contain(" "));
    }

    [Test]
    public async Task SaveVideoAsync_TitleWithConsecutiveSpaces_CollapsesToSingleDash()
    {
        var mockFile = CreateMockFormFile("video.mp4", "content");
        
        var result = await _service.SaveVideoAsync(mockFile.Object, "My    Test    Video");

        Assert.That(result.Filename, Does.Contain("my-test-video"));
        Assert.That(result.Filename, Does.Not.Match(".*--.*")); // No consecutive dashes
    }
}

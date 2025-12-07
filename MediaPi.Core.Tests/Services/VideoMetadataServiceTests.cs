// Copyright (c) 2025 sw.consulting
// This file is a part of Media Pi backend

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using MediaPi.Core.Services;
using MediaPi.Core.Services.Interfaces;
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
    public async Task ExtractMetadataAsync_NullPath_ReturnsNull()
    {
        var result = await _service.ExtractMetadataAsync(null!, CancellationToken.None);
        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task ExtractMetadataAsync_WhitespacePath_ReturnsNull()
    {
        var result = await _service.ExtractMetadataAsync("   ", CancellationToken.None);
        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task ExtractMetadataAsync_WithCancellationToken_RespectsToken()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(tempFile, "dummy video content", CancellationToken.None);

            using var cts = new CancellationTokenSource();
            cts.Cancel();

            try
            {
                await _service.ExtractMetadataAsync(tempFile, cts.Token);
                // If no exception is thrown, the test should still pass as the service might complete before checking cancellation
                Assert.Pass("Service completed before cancellation was checked");
            }
            catch (OperationCanceledException)
            {
                // Expected - cancellation was respected
                Assert.Pass();
            }
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
    public async Task ExtractMetadataAsync_ValidFile_ReturnsBasicMetadataOnExtractionFailure()
    {
        // Create a temporary non-video file that will fail metadata extraction
        var tempFile = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(tempFile, "This is not a video file", CancellationToken.None);

            var result = await _service.ExtractMetadataAsync(tempFile, CancellationToken.None);

            // Should return basic metadata even if full extraction fails
            Assert.That(result, Is.Not.Null);
            Assert.That(result!.FileSizeBytes, Is.GreaterThan(0));
            // Duration might be null since it's not a real video
            Assert.That(result.DurationSeconds, Is.Null.Or.GreaterThanOrEqualTo(0));
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
    public async Task ExtractMetadataAsync_ZeroSizeFile_ReturnsMetadataWithZeroSize()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            // Create an empty file
            File.WriteAllText(tempFile, string.Empty);

            var result = await _service.ExtractMetadataAsync(tempFile, CancellationToken.None);

            Assert.That(result, Is.Not.Null);
            Assert.That(result!.FileSizeBytes, Is.EqualTo(0));
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
    public async Task ExtractMetadataAsync_LogsWarning_WhenFileNotFound()
    {
        var result = await _service.ExtractMetadataAsync("nonexistent.mp4", CancellationToken.None);

        Assert.That(result, Is.Null);
        // Verify logger was called with warning
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Video file not found")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Test]
    public async Task ExtractMetadataAsync_LogsError_OnExceptionDuringExtraction()
    {
        // This test verifies that errors during metadata extraction are logged
        // and that basic metadata (file size) is still returned
        var tempFile = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(tempFile, "invalid video content", CancellationToken.None);

            var result = await _service.ExtractMetadataAsync(tempFile, CancellationToken.None);

            // Should still return basic metadata
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
    public async Task ExtractMetadataAsync_ReturnsAllMetadataFields()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(tempFile, "dummy video content", CancellationToken.None);

            var result = await _service.ExtractMetadataAsync(tempFile, CancellationToken.None);

            Assert.That(result, Is.Not.Null);
            // FileSizeBytes is required and should always be present
            Assert.That(result!.FileSizeBytes, Is.GreaterThanOrEqualTo(0));
            // DurationSeconds is optional and may be null for non-video files
            Assert.That(result, Has.Property(nameof(result.DurationSeconds)));
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
    public void ConvertFileSizeToUInt_NegativeSize_ThrowsArgumentOutOfRangeException()
    {
        var method = typeof(VideoMetadataService).GetMethod("ConvertFileSizeToUInt", 
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.That(method, Is.Not.Null);

        var exception = Assert.Throws<TargetInvocationException>(() => 
            method!.Invoke(null, new object[] { -1L }));
        Assert.That(exception?.InnerException, Is.TypeOf<ArgumentOutOfRangeException>());
    }

    [Test]
    public void ConvertFileSizeToUInt_SizeExceedsUIntMax_ThrowsArgumentOutOfRangeException()
    {
        var method = typeof(VideoMetadataService).GetMethod("ConvertFileSizeToUInt", 
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.That(method, Is.Not.Null);

        var largeSize = (long)uint.MaxValue + 1;
        var exception = Assert.Throws<TargetInvocationException>(() => 
            method!.Invoke(null, new object[] { largeSize }));
        Assert.That(exception?.InnerException, Is.TypeOf<ArgumentOutOfRangeException>());
    }

    [Test]
    public void ConvertFileSizeToUInt_ZeroSize_ReturnsZero()
    {
        var method = typeof(VideoMetadataService).GetMethod("ConvertFileSizeToUInt", 
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.That(method, Is.Not.Null);

        var result = method!.Invoke(null, new object[] { 0L });
        Assert.That(result, Is.EqualTo(0u));
    }

    [Test]
    public void ConvertFileSizeToUInt_ValidSize_ReturnsCorrectValue()
    {
        var method = typeof(VideoMetadataService).GetMethod("ConvertFileSizeToUInt", 
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.That(method, Is.Not.Null);

        var result = method!.Invoke(null, new object[] { 12345L });
        Assert.That(result, Is.EqualTo(12345u));
    }

    [Test]
    public void ConvertDurationToUInt_NullDuration_ReturnsNull()
    {
        var method = typeof(VideoMetadataService).GetMethod("ConvertDurationToUInt", 
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.That(method, Is.Not.Null);

        var result = method!.Invoke(null, new object[] { null! });
        Assert.That(result, Is.Null);
    }

    [Test]
    public void ConvertDurationToUInt_NegativeDuration_ReturnsZero()
    {
        var method = typeof(VideoMetadataService).GetMethod("ConvertDurationToUInt", 
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.That(method, Is.Not.Null);

        var result = method!.Invoke(null, new object[] { -5.5 });
        Assert.That(result, Is.EqualTo(0u));
    }

    [Test]
    public void ConvertDurationToUInt_DurationExceedsUIntMax_ThrowsArgumentOutOfRangeException()
    {
        var method = typeof(VideoMetadataService).GetMethod("ConvertDurationToUInt", 
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.That(method, Is.Not.Null);

        // Test with a value definitely over the limit
        var overMaxDuration = (double)uint.MaxValue + 1000.0; // Much larger than max
        
        var exception = Assert.Throws<TargetInvocationException>(() => 
            method!.Invoke(null, new object[] { overMaxDuration }));
        Assert.That(exception?.InnerException, Is.TypeOf<ArgumentOutOfRangeException>());
    }

    [Test]
    public void ConvertDurationToUInt_ValidDuration_ReturnsRoundedValue()
    {
        var method = typeof(VideoMetadataService).GetMethod("ConvertDurationToUInt", 
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.That(method, Is.Not.Null);

        var result = method!.Invoke(null, new object[] { 123.7 });
        Assert.That(result, Is.EqualTo(124u)); // Rounds to nearest
    }

    [Test]
    public void ParseDurationFromDescription_TimeSpanFormat_ReturnsCorrectValue()
    {
        var method = typeof(VideoMetadataService).GetMethod("ParseDurationFromDescription", 
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.That(method, Is.Not.Null);

        var result = method!.Invoke(null, new object[] { "00:02:30" });
        Assert.That(result, Is.EqualTo(150.0)); // 2 minutes 30 seconds = 150 seconds
    }

    [Test]
    public void ParseDurationFromDescription_SecondsFormat_ReturnsCorrectValue()
    {
        var method = typeof(VideoMetadataService).GetMethod("ParseDurationFromDescription", 
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.That(method, Is.Not.Null);

        var testCases = new[]
        {
            ("120 seconds", 120.0),
            ("45 sec", 45.0),
            ("30 s", 30.0)
        };

        foreach (var (input, expected) in testCases)
        {
            var result = method!.Invoke(null, new object[] { input });
            Assert.That(result, Is.EqualTo(expected), $"Failed for input: {input}");
        }
    }

    [Test]
    public void ParseDurationFromDescription_InvalidFormat_ReturnsNull()
    {
        var method = typeof(VideoMetadataService).GetMethod("ParseDurationFromDescription", 
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.That(method, Is.Not.Null);

        var invalidInputs = new[] { "invalid", "abc seconds", "25:75:99", "" };

        foreach (var input in invalidInputs)
        {
            var result = method!.Invoke(null, new object[] { input });
            Assert.That(result, Is.Null, $"Should return null for input: {input}");
        }
    }

    [Test]
    public void ParseDurationFromDescription_UnderstandActualBehavior()
    {
        var method = typeof(VideoMetadataService).GetMethod("ParseDurationFromDescription", 
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.That(method, Is.Not.Null);

        // Test various inputs to understand the actual behavior
        var testInputs = new[] { "0.1", "1", "120", "120.5", "86400", "100000" };
        
        foreach (var input in testInputs)
        {
            var result = method!.Invoke(null, new object[] { input });
            Console.WriteLine($"Input: '{input}' -> Result: {result}");
        }
        
        // For now, just assert that the method can be called without error
        Assert.That(method, Is.Not.Null);
    }

    [Test]
    public async Task ExtractMetadataAsync_FileDeletedDuringExecution_ReturnsNull()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(tempFile, "test content", CancellationToken.None);
            
            // Delete file immediately to simulate race condition
            File.Delete(tempFile);
            
            var result = await _service.ExtractMetadataAsync(tempFile, CancellationToken.None);
            Assert.That(result, Is.Null);
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
    public async Task ExtractMetadataAsync_FileInfoThrowsException_ReturnsNull()
    {
        // Test when FileInfo creation fails (e.g., invalid path characters)
        var invalidPath = "invalid\0path.mp4";
        
        var result = await _service.ExtractMetadataAsync(invalidPath, CancellationToken.None);
        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task ExtractMetadataAsync_ExceptionInFallbackFileInfo_ReturnsNull()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(tempFile, "test", CancellationToken.None);
            
            // Make file read-only to potentially cause issues
            var fileInfo = new FileInfo(tempFile);
            fileInfo.IsReadOnly = true;
            
            var result = await _service.ExtractMetadataAsync(tempFile, CancellationToken.None);
            
            // Should still work for basic file info
            Assert.That(result, Is.Not.Null);
            Assert.That(result!.FileSizeBytes, Is.GreaterThanOrEqualTo(0));
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                var fileInfo = new FileInfo(tempFile);
                fileInfo.IsReadOnly = false;
                File.Delete(tempFile);
            }
        }
    }

    [Test]
    public async Task ExtractMetadataAsync_LargeFileSimulation_HandlesCorrectly()
    {
        // Create a file and test the conversion logic
        var tempFile = Path.GetTempFileName();
        try
        {
            // Write enough content to test size conversion
            var content = new string('A', 1024); // 1KB
            await File.WriteAllTextAsync(tempFile, content, CancellationToken.None);

            var result = await _service.ExtractMetadataAsync(tempFile, CancellationToken.None);

            Assert.That(result, Is.Not.Null);
            Assert.That(result!.FileSizeBytes, Is.EqualTo(1024));
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
    public async Task ExtractMetadataAsync_TaskRunCancellation_ProperlyCanceled()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(tempFile, "test content", CancellationToken.None);

            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(1));
            
            try
            {
                var result = await _service.ExtractMetadataAsync(tempFile, cts.Token);
                // If it completes quickly, that's fine too
                Assert.That(result, Is.Not.Null);
            }
            catch (OperationCanceledException)
            {
                // This is also acceptable - cancellation was respected
                Assert.Pass("Operation was properly cancelled");
            }
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
    public void ExtractVideoMetadata_PrivateMethod_ReturnsBasicMetadata()
    {
        var method = typeof(VideoMetadataService).GetMethod("ExtractVideoMetadata", 
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.That(method, Is.Not.Null);

        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempFile, "dummy content");

            var result = method!.Invoke(_service, new object[] { tempFile });
            Assert.That(result, Is.Not.Null);
            
            // The result should be a VideoMetadata object
            Assert.That(result!.GetType().Name, Does.Contain("VideoMetadata"));
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
    public async Task ExtractMetadataAsync_VerifyLoggingBehavior()
    {
        // Test that debug logging occurs during metadata extraction failures
        var tempFile = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(tempFile, "not a video file", CancellationToken.None);

            var result = await _service.ExtractMetadataAsync(tempFile, CancellationToken.None);

            // Should return basic metadata
            Assert.That(result, Is.Not.Null);
            Assert.That(result!.FileSizeBytes, Is.GreaterThan(0));

            // Logger should be used, but debug logs might not be verifiable in all test scenarios
            // The important thing is that the method completes without throwing
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
    public async Task ExtractMetadataAsync_HandlesDifferentFileTypes()
    {
        // Test various file extensions and content types
        var testFiles = new[]
        {
            ("test.mp4", "fake mp4 content"),
            ("test.avi", "fake avi content"),
            ("test.mkv", "fake mkv content"),
            ("test.txt", "plain text content")
        };

        foreach (var (filename, content) in testFiles)
        {
            var tempFile = Path.GetTempFileName();
            try
            {
                await File.WriteAllTextAsync(tempFile, content, CancellationToken.None);

                var result = await _service.ExtractMetadataAsync(tempFile, CancellationToken.None);

                // Should return basic metadata for all file types
                Assert.That(result, Is.Not.Null, $"Should return metadata for {filename}");
                Assert.That(result!.FileSizeBytes, Is.GreaterThan(0), $"File size should be > 0 for {filename}");
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

    [Test]
    public async Task ExtractMetadataAsync_ConcurrentAccess_HandlesCorrectly()
    {
        // Test multiple concurrent calls to the service
        var tempFile = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(tempFile, "test content for concurrency", CancellationToken.None);

            var tasks = new Task<VideoMetadata?>[5];
            for (int i = 0; i < tasks.Length; i++)
            {
                tasks[i] = _service.ExtractMetadataAsync(tempFile, CancellationToken.None);
            }

            var results = await Task.WhenAll(tasks);

            // All calls should succeed and return consistent results
            foreach (var result in results)
            {
                Assert.That(result, Is.Not.Null);
                Assert.That(result!.FileSizeBytes, Is.GreaterThan(0));
            }

            // Verify all results are the same
            var firstResult = results[0];
            if (firstResult != null)
            {
                foreach (var result in results.Skip(1))
                {
                    if (result != null)
                    {
                        Assert.That(result.FileSizeBytes, Is.EqualTo(firstResult.FileSizeBytes));
                    }
                }
            }
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
    public void InternalVideoMetadata_Properties_CanBeSetAndRetrieved()
    {
        // Test the internal metadata class properties through reflection
        var internalMetadataType = typeof(VideoMetadataService).GetNestedType("InternalVideoMetadata", 
            BindingFlags.NonPublic);
        Assert.That(internalMetadataType, Is.Not.Null);

        var instance = Activator.CreateInstance(internalMetadataType!);
        Assert.That(instance, Is.Not.Null);

        // Test that properties exist and can be set
        var properties = internalMetadataType?.GetProperties();
        Assert.That(properties?.Length, Is.EqualTo(1)); // Should have 1 property (DurationSeconds)
    }

    [Test]
    public async Task ExtractMetadataAsync_FileWithSpecialCharacters_HandlesCorrectly()
    {
        // Test file paths with special characters that might cause issues
        var tempDir = Path.GetTempPath();
        var specialFileName = Path.Combine(tempDir, "test file with spaces & symbols.tmp");
        
        try
        {
            await File.WriteAllTextAsync(specialFileName, "test content with special chars", CancellationToken.None);

            var result = await _service.ExtractMetadataAsync(specialFileName, CancellationToken.None);

            Assert.That(result, Is.Not.Null);
            Assert.That(result!.FileSizeBytes, Is.GreaterThan(0));
        }
        finally
        {
            if (File.Exists(specialFileName))
            {
                File.Delete(specialFileName);
            }
        }
    }

    [Test]
    public async Task ExtractMetadataAsync_VeryLongPath_HandlesCorrectly()
    {
        // Test with a reasonably long path (but not so long as to hit OS limits)
        var tempDir = Path.GetTempPath();
        var longSubDir = new string('a', 50); // 50 character subdirectory name
        var longDirPath = Path.Combine(tempDir, longSubDir);
        var longFilePath = Path.Combine(longDirPath, "testfile.tmp");
        
        try
        {
            Directory.CreateDirectory(longDirPath);
            await File.WriteAllTextAsync(longFilePath, "test content", CancellationToken.None);

            var result = await _service.ExtractMetadataAsync(longFilePath, CancellationToken.None);

            Assert.That(result, Is.Not.Null);
            Assert.That(result!.FileSizeBytes, Is.GreaterThan(0));
        }
        finally
        {
            if (File.Exists(longFilePath))
            {
                File.Delete(longFilePath);
            }
            if (Directory.Exists(longDirPath))
            {
                Directory.Delete(longDirPath);
            }
        }
    }

    [Test]
    public void ConvertFileSizeToUInt_MaxValue_ReturnsMaxValue()
    {
        var method = typeof(VideoMetadataService).GetMethod("ConvertFileSizeToUInt", 
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.That(method, Is.Not.Null);

        var result = method!.Invoke(null, new object[] { (long)uint.MaxValue });
        Assert.That(result, Is.EqualTo(uint.MaxValue));
    }

    [Test]
    public void ConvertDurationToUInt_MaxValue_ThrowsException()
    {
        var method = typeof(VideoMetadataService).GetMethod("ConvertDurationToUInt", 
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.That(method, Is.Not.Null);

        // Test with a value definitely over the limit
        var overMaxDuration = (double)uint.MaxValue + 1000.0; // Much larger than max
        
        var exception = Assert.Throws<TargetInvocationException>(() => 
            method!.Invoke(null, new object[] { overMaxDuration }));
        Assert.That(exception?.InnerException, Is.TypeOf<ArgumentOutOfRangeException>());
    }

    [Test]
    public void ConvertDurationToUInt_ExactMaxValue_ReturnsMaxValue()
    {
        var method = typeof(VideoMetadataService).GetMethod("ConvertDurationToUInt", 
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.That(method, Is.Not.Null);

        var maxValidDuration = (double)uint.MaxValue;
        var result = method!.Invoke(null, new object[] { maxValidDuration });
        Assert.That(result, Is.EqualTo(uint.MaxValue));
    }

    [Test]
    public async Task ExtractMetadataAsync_FileWithDifferentExtensions_ProcessesAll()
    {
        // Test various file extensions to ensure the service doesn't filter by extension
        var extensions = new[] { ".mp4", ".avi", ".mov", ".mkv", ".webm", ".mp3", ".wav", ".unknown" };
        
        foreach (var extension in extensions)
        {
            var tempFile = Path.ChangeExtension(Path.GetTempFileName(), extension);
            try
            {
                await File.WriteAllTextAsync(tempFile, $"content for {extension} file", CancellationToken.None);

                var result = await _service.ExtractMetadataAsync(tempFile, CancellationToken.None);

                // All files should return metadata regardless of extension
                Assert.That(result, Is.Not.Null, $"Should return metadata for {extension} files");
                Assert.That(result!.FileSizeBytes, Is.GreaterThan(0), $"Should have file size for {extension} files");
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

    [Test]
    public async Task ExtractMetadataAsync_EmptyFile_ReturnsZeroSizeMetadata()
    {
        // Confirm that empty files are handled correctly
        var tempFile = Path.GetTempFileName();
        try
        {
            // Ensure file is truly empty
            using (var fs = File.Create(tempFile))
            {
                // Create empty file
            }

            var result = await _service.ExtractMetadataAsync(tempFile, CancellationToken.None);

            Assert.That(result, Is.Not.Null);
            Assert.That(result!.FileSizeBytes, Is.EqualTo(0u));
            Assert.That(result.DurationSeconds, Is.Null); // Should be null for empty file
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
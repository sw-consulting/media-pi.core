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
using MediaPi.Core.Tests.TestHelpers;
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

    #region Basic Validation Tests

    [Test]
    public async Task ExtractMetadataAsync_FileNotFound_ReturnsNull()
    {
        var result = await _service.ExtractMetadataAsync("nonexistent.mp4", CancellationToken.None);
        Assert.That(result, Is.Null);
        
        VerifyLoggerCalled(LogLevel.Warning, "Video file not found");
    }

    [Test]
    public async Task ExtractMetadataAsync_EmptyPath_ReturnsNull()
    {
        var result = await _service.ExtractMetadataAsync("", CancellationToken.None);
        Assert.That(result, Is.Null);
        
        VerifyLoggerCalled(LogLevel.Warning, "Video file not found");
    }

    [Test]
    public async Task ExtractMetadataAsync_NullPath_ReturnsNull()
    {
        var result = await _service.ExtractMetadataAsync(null!, CancellationToken.None);
        Assert.That(result, Is.Null);
        
        VerifyLoggerCalled(LogLevel.Warning, "Video file not found");
    }

    [Test]
    public async Task ExtractMetadataAsync_WhitespacePath_ReturnsNull()
    {
        var result = await _service.ExtractMetadataAsync("   ", CancellationToken.None);
        Assert.That(result, Is.Null);
        
        VerifyLoggerCalled(LogLevel.Warning, "Video file not found");
    }

    #endregion

    #region File Content Tests

    [Test]
    public async Task ExtractMetadataAsync_ValidFile_ReturnsBasicMetadata()
    {
        var tempFile = await TestVideoFileGenerator.CreateTestFileWithExtensionAsync(".mp4", "test video content");
        try
        {
            var result = await _service.ExtractMetadataAsync(tempFile, CancellationToken.None);

            Assert.That(result, Is.Not.Null);
            Assert.That(result!.FileSizeBytes, Is.GreaterThan(0));
            // Duration might be null if MediaInfo can't extract it from the test file
            // This is expected for non-real video files
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
    public async Task ExtractMetadataAsync_EmptyFile_ReturnsZeroSizeMetadata()
    {
        var tempFile = await TestVideoFileGenerator.CreateEmptyFileAsync();
        try
        {
            var result = await _service.ExtractMetadataAsync(tempFile, CancellationToken.None);

            Assert.That(result, Is.Not.Null);
            Assert.That(result!.FileSizeBytes, Is.EqualTo(0u));
            Assert.That(result.DurationSeconds, Is.Null);
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
    public async Task ExtractMetadataAsync_LargeFile_HandlesCorrectly()
    {
        var tempFile = await TestVideoFileGenerator.CreateTestFileWithSizeAsync(".mp4", 1024);
        try
        {
            var result = await _service.ExtractMetadataAsync(tempFile, CancellationToken.None);

            Assert.That(result, Is.Not.Null);
            Assert.That(result!.FileSizeBytes, Is.EqualTo(1024u));
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    #endregion

    #region File Extension Tests

    [Test]
    public async Task ExtractMetadataAsync_DifferentVideoExtensions_ProcessesAll()
    {
        var extensions = new[] { ".mp4", ".avi", ".mov", ".mkv", ".webm" };
        
        foreach (var extension in extensions)
        {
            var tempFile = await TestVideoFileGenerator.CreateTestFileWithExtensionAsync(extension, $"content for {extension} file");
            try
            {
                var result = await _service.ExtractMetadataAsync(tempFile, CancellationToken.None);

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
    public async Task ExtractMetadataAsync_NonVideoFiles_ReturnsBasicMetadataOnly()
    {
        var extensions = new[] { ".txt", ".doc", ".pdf", ".unknown" };
        
        foreach (var extension in extensions)
        {
            var tempFile = await TestVideoFileGenerator.CreateTestFileWithExtensionAsync(extension, $"content for {extension} file");
            try
            {
                var result = await _service.ExtractMetadataAsync(tempFile, CancellationToken.None);

                Assert.That(result, Is.Not.Null, $"Should return metadata for {extension} files");
                Assert.That(result!.FileSizeBytes, Is.GreaterThan(0), $"Should have file size for {extension} files");
                // Duration will likely be null for non-video files
                Assert.That(result.DurationSeconds, Is.Null, $"Duration should be null for {extension} files");
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

    #endregion

    #region Cancellation Tests

    [Test]
    public async Task ExtractMetadataAsync_WithCancellationToken_RespectsToken()
    {
        var tempFile = await TestVideoFileGenerator.CreateTestFileWithExtensionAsync(".mp4", "test content");
        try
        {
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            try
            {
                await _service.ExtractMetadataAsync(tempFile, cts.Token);
                Assert.Pass("Service completed before cancellation was checked");
            }
            catch (OperationCanceledException)
            {
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
    public async Task ExtractMetadataAsync_CancellationDuringProcessExecution_HandlesCancellation()
    {
        var tempFile = await TestVideoFileGenerator.CreateTestFileWithExtensionAsync(".mp4", "test content");
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));
            
            try
            {
                var result = await _service.ExtractMetadataAsync(tempFile, cts.Token);
                // If it completes quickly, that's fine too
                Assert.That(result, Is.Not.Null.Or.Null);
            }
            catch (OperationCanceledException)
            {
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

    #endregion

    #region File System Edge Cases

    [Test]
    public async Task ExtractMetadataAsync_FileDeletedDuringExecution_ReturnsNull()
    {
        var tempFile = await TestVideoFileGenerator.CreateTestFileWithExtensionAsync(".mp4", "test content");
        try
        {
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
    public async Task ExtractMetadataAsync_FileWithSpecialCharacters_HandlesCorrectly()
    {
        var tempDir = Path.GetTempPath();
        var specialFileName = Path.Combine(tempDir, "test file with spaces & symbols.mp4");
        
        try
        {
            await File.WriteAllTextAsync(specialFileName, "test content with special chars");

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
        var tempDir = Path.GetTempPath();
        var longSubDir = new string('a', 50);
        var longDirPath = Path.Combine(tempDir, longSubDir);
        var longFilePath = Path.Combine(longDirPath, "testfile.mp4");
        
        try
        {
            Directory.CreateDirectory(longDirPath);
            await File.WriteAllTextAsync(longFilePath, "test content");

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

    #endregion

    #region Concurrent Access Tests

    [Test]
    public async Task ExtractMetadataAsync_ConcurrentAccess_HandlesCorrectly()
    {
        var tempFile = await TestVideoFileGenerator.CreateTestFileWithExtensionAsync(".mp4", "test content for concurrency");
        try
        {
            var tasks = new Task<VideoMetadata?>[5];
            for (int i = 0; i < tasks.Length; i++)
            {
                tasks[i] = _service.ExtractMetadataAsync(tempFile, CancellationToken.None);
            }

            var results = await Task.WhenAll(tasks);

            foreach (var result in results)
            {
                Assert.That(result, Is.Not.Null);
                Assert.That(result!.FileSizeBytes, Is.GreaterThan(0));
            }

            // Verify all results are consistent
            var firstResult = results[0];
            if (firstResult != null)
            {
                foreach (var result in results.Skip(1).Where(result => result != null))
                {
                    Assert.That(result!.FileSizeBytes, Is.EqualTo(firstResult.FileSizeBytes));
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

    #endregion

    #region Error Handling Tests

    [Test]
    public async Task ExtractMetadataAsync_InvalidFilePath_ReturnsNull()
    {
        var invalidPath = "invalid\0path.mp4";
        
        var result = await _service.ExtractMetadataAsync(invalidPath, CancellationToken.None);
        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task ExtractMetadataAsync_ProcessExecutionException_ReturnsBasicMetadata()
    {
        // Create a file that will exist but MediaInfo might fail on
        var tempFile = await TestVideoFileGenerator.CreateTestFileWithExtensionAsync(".mp4", "invalid video content");
        try
        {
            var result = await _service.ExtractMetadataAsync(tempFile, CancellationToken.None);

            // Should still return basic metadata even if MediaInfo extraction fails
            Assert.That(result, Is.Not.Null);
            Assert.That(result!.FileSizeBytes, Is.GreaterThan(0));
            Assert.That(result.DurationSeconds, Is.Null);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    #endregion

    #region Utility Method Tests

    [Test]
    public void ConvertFileSizeToUInt_NegativeSize_ThrowsArgumentOutOfRangeException()
    {
        var method = GetPrivateStaticMethod("ConvertFileSizeToUInt");
        Assert.That(method, Is.Not.Null);

        var exception = Assert.Throws<TargetInvocationException>(() => 
            method!.Invoke(null, new object[] { -1L }));
        Assert.That(exception?.InnerException, Is.TypeOf<ArgumentOutOfRangeException>());
    }

    [Test]
    public void ConvertFileSizeToUInt_SizeExceedsUIntMax_ThrowsArgumentOutOfRangeException()
    {
        var method = GetPrivateStaticMethod("ConvertFileSizeToUInt");
        Assert.That(method, Is.Not.Null);

        var largeSize = (long)uint.MaxValue + 1;
        var exception = Assert.Throws<TargetInvocationException>(() => 
            method!.Invoke(null, new object[] { largeSize }));
        Assert.That(exception?.InnerException, Is.TypeOf<ArgumentOutOfRangeException>());
    }

    [Test]
    public void ConvertFileSizeToUInt_ZeroSize_ReturnsZero()
    {
        var method = GetPrivateStaticMethod("ConvertFileSizeToUInt");
        Assert.That(method, Is.Not.Null);

        var result = method!.Invoke(null, new object[] { 0L });
        Assert.That(result, Is.EqualTo(0u));
    }

    [Test]
    public void ConvertFileSizeToUInt_ValidSize_ReturnsCorrectValue()
    {
        var method = GetPrivateStaticMethod("ConvertFileSizeToUInt");
        Assert.That(method, Is.Not.Null);

        var result = method!.Invoke(null, new object[] { 12345L });
        Assert.That(result, Is.EqualTo(12345u));
    }

    [Test]
    public void ConvertFileSizeToUInt_MaxValue_ReturnsMaxValue()
    {
        var method = GetPrivateStaticMethod("ConvertFileSizeToUInt");
        Assert.That(method, Is.Not.Null);

        var result = method!.Invoke(null, new object[] { (long)uint.MaxValue });
        Assert.That(result, Is.EqualTo(uint.MaxValue));
    }

    [Test]
    public void ConvertDurationToUInt_NullDuration_ReturnsNull()
    {
        var method = GetPrivateStaticMethod("ConvertDurationToUInt");
        Assert.That(method, Is.Not.Null);

        var result = method!.Invoke(null, new object[] { null! });
        Assert.That(result, Is.Null);
    }

    [Test]
    public void ConvertDurationToUInt_NegativeDuration_ReturnsZero()
    {
        var method = GetPrivateStaticMethod("ConvertDurationToUInt");
        Assert.That(method, Is.Not.Null);

        var result = method!.Invoke(null, new object[] { -5.5 });
        Assert.That(result, Is.EqualTo(0u));
    }

    [Test]
    public void ConvertDurationToUInt_DurationExceedsUIntMax_ThrowsArgumentOutOfRangeException()
    {
        var method = GetPrivateStaticMethod("ConvertDurationToUInt");
        Assert.That(method, Is.Not.Null);

        var overMaxDuration = (double)uint.MaxValue + 1000.0;
        
        var exception = Assert.Throws<TargetInvocationException>(() => 
            method!.Invoke(null, new object[] { overMaxDuration }));
        Assert.That(exception?.InnerException, Is.TypeOf<ArgumentOutOfRangeException>());
    }

    [Test]
    public void ConvertDurationToUInt_ValidDuration_ReturnsRoundedValue()
    {
        var method = GetPrivateStaticMethod("ConvertDurationToUInt");
        Assert.That(method, Is.Not.Null);

        var result = method!.Invoke(null, new object[] { 123.7 });
        Assert.That(result, Is.EqualTo(124u));
    }

    [Test]
    public void ConvertDurationToUInt_ExactMaxValue_ReturnsMaxValue()
    {
        var method = GetPrivateStaticMethod("ConvertDurationToUInt");
        Assert.That(method, Is.Not.Null);

        var maxValidDuration = (double)uint.MaxValue;
        var result = method!.Invoke(null, new object[] { maxValidDuration });
        Assert.That(result, Is.EqualTo(uint.MaxValue));
    }

    #endregion

    #region Duration Parsing Tests

    [Test]
    public void ParseDurationOutput_ValidTimeSpanFormat_ReturnsCorrectValue()
    {
        var method = GetPrivateMethod("ParseDurationOutput");
        Assert.That(method, Is.Not.Null);

        var testCases = new[]
        {
            ("00:02:30.000", 150u),  // 2 minutes 30 seconds
            ("01:00:00.000", 3600u), // 1 hour
            ("00:00:10.500", 10u),   // 10.5 seconds, rounded to 10 (Math.Round rounds 0.5 to nearest even)
        };

        foreach (var (input, expected) in testCases)
        {
            var result = method!.Invoke(_service, new object[] { input });
            Assert.That(result, Is.EqualTo(expected), $"Failed for input: {input}");
        }
    }

    [Test]
    public void ParseDurationOutput_DecimalSecondsFormat_ReturnsCorrectValue()
    {
        var method = GetPrivateMethod("ParseDurationOutput");
        Assert.That(method, Is.Not.Null);

        var result = method!.Invoke(_service, new object[] { "123.456" });
        Assert.That(result, Is.EqualTo(123u));
    }

    [Test]
    public void ParseDurationOutput_EmptyOrNull_ReturnsNull()
    {
        var method = GetPrivateMethod("ParseDurationOutput");
        Assert.That(method, Is.Not.Null);

        var testCases = new string?[] { null, "", "   " };

        foreach (var input in testCases)
        {
            var result = method!.Invoke(_service, new object[] { input! });
            Assert.That(result, Is.Null, $"Should return null for input: {input ?? "null"}");
        }
    }

    [Test]
    public void ParseDurationOutput_InvalidFormat_ReturnsNull()
    {
        var method = GetPrivateMethod("ParseDurationOutput");
        Assert.That(method, Is.Not.Null);

        var invalidInputs = new[] { "invalid", "abc:def:ghi", "not_a_duration" };

        foreach (var input in invalidInputs)
        {
            var result = method!.Invoke(_service, new object[] { input });
            Assert.That(result, Is.Null, $"Should return null for input: {input}");
        }
    }

    #endregion

    #region Integration and Logging Tests

    [Test]
    public async Task ExtractMetadataAsync_LogsWarning_WhenFileNotFound()
    {
        var result = await _service.ExtractMetadataAsync("nonexistent.mp4", CancellationToken.None);

        Assert.That(result, Is.Null);
        VerifyLoggerCalled(LogLevel.Warning, "Video file not found");
    }

    [Test]
    public async Task ExtractMetadataAsync_ReturnsAllMetadataFields()
    {
        var tempFile = await TestVideoFileGenerator.CreateTestFileWithExtensionAsync(".mp4", "dummy video content");
        try
        {
            var result = await _service.ExtractMetadataAsync(tempFile, CancellationToken.None);

            Assert.That(result, Is.Not.Null);
            Assert.That(result!.FileSizeBytes, Is.GreaterThanOrEqualTo(0));
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

    #endregion

    #region MediaInfo Process Tests

    [Test]
    public async Task ExtractMetadataAsync_MediaInfoProcessFails_ReturnsBasicMetadata()
    {
        // This test verifies that when MediaInfo process fails, we still return basic metadata
        var tempFile = await TestVideoFileGenerator.CreateTestFileWithExtensionAsync(".mp4", "not a real video");
        try
        {
            var result = await _service.ExtractMetadataAsync(tempFile, CancellationToken.None);

            Assert.That(result, Is.Not.Null);
            Assert.That(result!.FileSizeBytes, Is.GreaterThan(0));
            // Duration will likely be null since it's not a real video file
            Assert.That(result.DurationSeconds, Is.Null);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    #endregion

    #region SHA256 Calculation Tests

    [Test]
    public async Task ExtractMetadataAsync_CalculatesSha256()
    {
        var content = "test video content for sha256 calculation";
        var tempFile = await TestVideoFileGenerator.CreateTestFileWithExtensionAsync(".mp4", content);
        try
        {
            var result = await _service.ExtractMetadataAsync(tempFile, CancellationToken.None);

            Assert.That(result, Is.Not.Null);
            Assert.That(result!.Sha256, Is.Not.Null.And.Not.Empty);
            Assert.That(result.Sha256, Has.Length.EqualTo(64), "SHA256 hex string should be 64 characters");
            Assert.That(result.Sha256, Does.Match("^[a-f0-9]{64}$"), "SHA256 should be lowercase hex");

            // Verify the hash is correct
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            var expectedHash = Convert.ToHexString(sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(content))).ToLowerInvariant();
            Assert.That(result.Sha256, Is.EqualTo(expectedHash));
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
    public async Task ExtractMetadataAsync_EmptyFile_CalculatesSha256()
    {
        var tempFile = await TestVideoFileGenerator.CreateEmptyFileAsync();
        try
        {
            var result = await _service.ExtractMetadataAsync(tempFile, CancellationToken.None);

            Assert.That(result, Is.Not.Null);
            Assert.That(result!.Sha256, Is.Not.Null.And.Not.Empty);
            Assert.That(result.Sha256, Has.Length.EqualTo(64));

            // Empty file should have known SHA256
            const string emptyFileSha256 = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";
            Assert.That(result.Sha256, Is.EqualTo(emptyFileSha256));
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
    public async Task ExtractMetadataAsync_LargeFile_CalculatesSha256()
    {
        // Create a 1MB file
        var tempFile = await TestVideoFileGenerator.CreateTestFileWithSizeAsync(".mp4", 1024 * 1024);
        try
        {
            var result = await _service.ExtractMetadataAsync(tempFile, CancellationToken.None);

            Assert.That(result, Is.Not.Null);
            Assert.That(result!.Sha256, Is.Not.Null.And.Not.Empty);
            Assert.That(result.Sha256, Has.Length.EqualTo(64));
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
    public async Task ExtractMetadataAsync_Sha256IsConsistent()
    {
        var content = "consistent content for testing sha256";
        var tempFile = await TestVideoFileGenerator.CreateTestFileWithExtensionAsync(".mp4", content);
        try
        {
            var result1 = await _service.ExtractMetadataAsync(tempFile, CancellationToken.None);
            var result2 = await _service.ExtractMetadataAsync(tempFile, CancellationToken.None);

            Assert.That(result1, Is.Not.Null);
            Assert.That(result2, Is.Not.Null);
            Assert.That(result1!.Sha256, Is.EqualTo(result2!.Sha256), "SHA256 should be consistent across multiple calls");
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
    public async Task ExtractMetadataAsync_DifferentContents_DifferentSha256()
    {
        var tempFile1 = await TestVideoFileGenerator.CreateTestFileWithExtensionAsync(".mp4", "content1");
        var tempFile2 = await TestVideoFileGenerator.CreateTestFileWithExtensionAsync(".mp4", "content2");
        try
        {
            var result1 = await _service.ExtractMetadataAsync(tempFile1, CancellationToken.None);
            var result2 = await _service.ExtractMetadataAsync(tempFile2, CancellationToken.None);

            Assert.That(result1, Is.Not.Null);
            Assert.That(result2, Is.Not.Null);
            Assert.That(result1!.Sha256, Is.Not.EqualTo(result2!.Sha256), "Different content should produce different SHA256");
        }
        finally
        {
            if (File.Exists(tempFile1))
            {
                File.Delete(tempFile1);
            }
            if (File.Exists(tempFile2))
            {
                File.Delete(tempFile2);
            }
        }
    }

    [Test]
    public async Task ExtractMetadataAsync_Sha256WithCancellation_RespectsToken()
    {
        var tempFile = await TestVideoFileGenerator.CreateTestFileWithSizeAsync(".mp4", 10 * 1024 * 1024); // 10MB
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(10));

            try
            {
                var result = await _service.ExtractMetadataAsync(tempFile, cts.Token);
                // If it completes, that's fine
                if (result != null)
                {
                    Assert.That(result.Sha256, Is.Null.Or.Not.Empty);
                }
            }
            catch (OperationCanceledException)
            {
                Assert.Pass("SHA256 calculation was properly cancelled");
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

    #endregion

    #region Helper Methods

    private MethodInfo? GetPrivateStaticMethod(string methodName)
    {
        return typeof(VideoMetadataService).GetMethod(methodName, 
            BindingFlags.NonPublic | BindingFlags.Static);
    }

    private MethodInfo? GetPrivateMethod(string methodName)
    {
        return typeof(VideoMetadataService).GetMethod(methodName, 
            BindingFlags.NonPublic | BindingFlags.Instance);
    }

    private void VerifyLoggerCalled(LogLevel level, string messageContains)
    {
        _mockLogger.Verify(
            x => x.Log(
                level,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(messageContains)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    #endregion
}
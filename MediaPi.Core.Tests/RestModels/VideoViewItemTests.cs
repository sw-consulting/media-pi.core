// Copyright (c) 2025 sw.consulting
// This file is a part of Media Pi backend

using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using MediaPi.Core.Models;
using MediaPi.Core.RestModels;
using NUnit.Framework;

namespace MediaPi.Core.Tests.RestModels;

[TestFixture]
public class VideoViewItemTests
{
    #region Constructor Tests

    [Test]
    public void Constructor_WithValidVideo_CopiesAllProperties()
    {
        // Arrange
        var video = CreateTestVideo(
            id: 123,
            title: "Test Video",
            filename: "test.mp4",
            originalFilename: "original_test.mp4",
            fileSizeBytes: 1048576, // 1 MB
            durationSeconds: 3661,   // 1 hour, 1 minute, 1 second
            accountId: 456
        );

        // Act
        var viewItem = new VideoViewItem(video);

        // Assert
        Assert.That(viewItem.Id, Is.EqualTo(123));
        Assert.That(viewItem.Title, Is.EqualTo("Test Video"));
        Assert.That(viewItem.Filename, Is.EqualTo("test.mp4"));
        Assert.That(viewItem.OriginalFilename, Is.EqualTo("original_test.mp4"));
        Assert.That(viewItem.FileSizeBytes, Is.EqualTo(1048576));
        Assert.That(viewItem.DurationSeconds, Is.EqualTo(3661));
        Assert.That(viewItem.AccountId, Is.EqualTo(456));
    }

    [Test]
    public void Constructor_WithNullDuration_HandlesCorrectly()
    {
        // Arrange
        var video = CreateTestVideo(durationSeconds: null);

        // Act
        var viewItem = new VideoViewItem(video);

        // Assert
        Assert.That(viewItem.DurationSeconds, Is.Null);
        Assert.That(viewItem.Duration, Is.EqualTo("не известно"));
    }

    [Test]
    public void Constructor_WithZeroFileSize_HandlesCorrectly()
    {
        // Arrange
        var video = CreateTestVideo(fileSizeBytes: 0);

        // Act
        var viewItem = new VideoViewItem(video);

        // Assert
        Assert.That(viewItem.FileSizeBytes, Is.EqualTo(0));
        Assert.That(viewItem.FileSize, Is.EqualTo("0 байт"));
    }

    [Test]
    public void Constructor_WithMaxValues_HandlesCorrectly()
    {
        // Arrange
        var video = CreateTestVideo(
            fileSizeBytes: uint.MaxValue,
            durationSeconds: uint.MaxValue
        );

        // Act
        var viewItem = new VideoViewItem(video);

        // Assert
        Assert.That(viewItem.FileSizeBytes, Is.EqualTo(uint.MaxValue));
        Assert.That(viewItem.DurationSeconds, Is.EqualTo(uint.MaxValue));
        Assert.That(viewItem.FileSize, Contains.Substring("Гб")); // uint.MaxValue is ~4GB, not TB
        Assert.That(viewItem.Duration, Does.Match(@"\d+:\d{2}:\d{2}")); // Should be HH:mm:ss format
    }

    #endregion

    #region File Size Formatting Tests

    [TestCase(0u, "0 байт")]
    [TestCase(1u, "1 байт")]
    [TestCase(500u, "500 байт")]
    [TestCase(1023u, "1023 байт")]
    public void FileSize_SmallSizes_FormatsAsBytes(uint sizeBytes, string expected)
    {
        // Arrange
        var video = CreateTestVideo(fileSizeBytes: sizeBytes);

        // Act
        var viewItem = new VideoViewItem(video);

        // Assert
        Assert.That(viewItem.FileSize, Is.EqualTo(expected));
    }

    [TestCase(1024u, "1 Кб")]
    [TestCase(1536u, "2 Кб")]  // 1.5KB rounds to 2KB
    [TestCase(2048u, "2 Кб")]
    [TestCase(10240u, "10 Кб")]
    [TestCase(1048575u, "1024 Кб")] // Just under 1MB
    public void FileSize_KilobyteRange_FormatsAsKilobytes(uint sizeBytes, string expected)
    {
        // Arrange
        var video = CreateTestVideo(fileSizeBytes: sizeBytes);

        // Act
        var viewItem = new VideoViewItem(video);

        // Assert
        Assert.That(viewItem.FileSize, Is.EqualTo(expected));
    }

    [TestCase(1048576u, "1.00 Мб")]      // 1 MB
    [TestCase(1572864u, "1.50 Мб")]     // 1.5 MB
    [TestCase(2097152u, "2.00 Мб")]     // 2 MB
    [TestCase(10485760u, "10.00 Мб")]   // 10 MB
    [TestCase(104857600u, "100.00 Мб")] // 100 MB
    [TestCase(1073741823u, "1024.00 Мб")] // Just under 1GB
    public void FileSize_MegabyteRange_FormatsAsMegabytes(uint sizeBytes, string expected)
    {
        // Arrange
        var video = CreateTestVideo(fileSizeBytes: sizeBytes);

        // Act
        var viewItem = new VideoViewItem(video);

        // Assert
        Assert.That(viewItem.FileSize, Is.EqualTo(expected));
    }

    [TestCase(1073741824u, "1.00 Гб")]   // 1 GB
    [TestCase(1610612736u, "1.50 Гб")]  // 1.5 GB
    [TestCase(2147483648u, "2.00 Гб")]  // 2 GB
    [TestCase(3221225472u, "3.00 Гб")]  // 3 GB
    [TestCase(4294967295u, "4.00 Гб")]  // uint.MaxValue (just under 4GB)
    public void FileSize_GigabyteRange_FormatsAsGigabytes(uint sizeBytes, string expected)
    {
        // Arrange
        var video = CreateTestVideo(fileSizeBytes: sizeBytes);

        // Act
        var viewItem = new VideoViewItem(video);

        // Assert
        Assert.That(viewItem.FileSize, Is.EqualTo(expected));
    }

    [Test]
    public void FileSize_VerifyRussianUnits()
    {
        // Test various units to ensure Russian localization
        var testCases = new[]
        {
            (0u, "байт"),
            (500u, "байт"),
            (1024u, "Кб"),
            (1048576u, "Мб"),
            (1073741824u, "Гб")
        };

        foreach (var (size, expectedUnit) in testCases)
        {
            var video = CreateTestVideo(fileSizeBytes: size);
            var viewItem = new VideoViewItem(video);
            
            Assert.That(viewItem.FileSize, Does.Contain(expectedUnit), 
                $"Size {size} should contain unit '{expectedUnit}'");
        }
    }

    #endregion

    #region Duration Formatting Tests

    [Test]
    public void Duration_NullValue_ReturnsUnknownInRussian()
    {
        // Arrange
        var video = CreateTestVideo(durationSeconds: null);

        // Act
        var viewItem = new VideoViewItem(video);

        // Assert
        Assert.That(viewItem.Duration, Is.EqualTo("не известно"));
    }

    [TestCase(0u, "00:00:00")]
    [TestCase(1u, "00:00:01")]
    [TestCase(59u, "00:00:59")]
    [TestCase(60u, "00:01:00")]
    [TestCase(61u, "00:01:01")]
    public void Duration_SecondsRange_FormatsCorrectly(uint seconds, string expected)
    {
        // Arrange
        var video = CreateTestVideo(durationSeconds: seconds);

        // Act
        var viewItem = new VideoViewItem(video);

        // Assert
        Assert.That(viewItem.Duration, Is.EqualTo(expected));
    }

    [TestCase(3600u, "01:00:00")]    // 1 hour
    [TestCase(3661u, "01:01:01")]    // 1 hour, 1 minute, 1 second
    [TestCase(7200u, "02:00:00")]    // 2 hours
    [TestCase(7323u, "02:02:03")]    // 2 hours, 2 minutes, 3 seconds
    [TestCase(36000u, "10:00:00")]   // 10 hours
    public void Duration_HoursRange_FormatsCorrectly(uint seconds, string expected)
    {
        // Arrange
        var video = CreateTestVideo(durationSeconds: seconds);

        // Act
        var viewItem = new VideoViewItem(video);

        // Assert
        Assert.That(viewItem.Duration, Is.EqualTo(expected));
    }

    [TestCase(86399u, "23:59:59")]   // Almost 24 hours
    [TestCase(86400u, "24:00:00")]   // Exactly 24 hours
    [TestCase(90000u, "25:00:00")]   // More than 24 hours
    [TestCase(359999u, "99:59:59")] // Large duration
    public void Duration_LargeDurations_FormatsCorrectly(uint seconds, string expected)
    {
        // Arrange
        var video = CreateTestVideo(durationSeconds: seconds);

        // Act
        var viewItem = new VideoViewItem(video);

        // Assert
        Assert.That(viewItem.Duration, Is.EqualTo(expected));
    }

    [Test]
    public void Duration_MaxValue_HandlesCorrectly()
    {
        // Arrange
        var video = CreateTestVideo(durationSeconds: uint.MaxValue);

        // Act
        var viewItem = new VideoViewItem(video);

        // Assert
        // uint.MaxValue = 4,294,967,295 seconds - just verify it formats correctly without overflow
        Assert.That(viewItem.Duration, Does.Match(@"\d+:\d{2}:\d{2}"));
        Assert.That(viewItem.Duration, Does.Not.Contain("-")); // Should not have negative values
    }

    [Test]
    public void Duration_VerifyHHmmssFormat()
    {
        // Test various durations to ensure consistent HH:mm:ss format
        var testDurations = new uint[] { 1, 60, 61, 3600, 3661, 86400 };

        foreach (var duration in testDurations)
        {
            var video = CreateTestVideo(durationSeconds: duration);
            var viewItem = new VideoViewItem(video);
            
            Assert.That(viewItem.Duration, Does.Match(@"\d{2}:\d{2}:\d{2}"), 
                $"Duration {duration} should match HH:mm:ss format");
        }
    }

    #endregion

    #region ToString and JSON Serialization Tests

    [Test]
    public void ToString_ReturnsValidJson()
    {
        // Arrange
        var video = CreateTestVideo();
        var viewItem = new VideoViewItem(video);

        // Act
        var jsonString = viewItem.ToString();

        // Assert
        Assert.That(jsonString, Is.Not.Null);
        Assert.That(jsonString, Is.Not.Empty);
        
        // Verify it's valid JSON by deserializing
        Assert.DoesNotThrow(() => JsonSerializer.Deserialize<object>(jsonString));
    }

    [Test]
    public void ToString_ContainsAllProperties()
    {
        // Arrange
        var video = CreateTestVideo(
            id: 123,
            title: "Test Video",
            fileSizeBytes: 1048576,
            durationSeconds: 3661
        );
        var viewItem = new VideoViewItem(video);

        // Act
        var jsonString = viewItem.ToString();

        // Assert
        Assert.That(jsonString, Does.Contain("123"));
        Assert.That(jsonString, Does.Contain("Test Video"));
        Assert.That(jsonString, Does.Contain("1048576"));
        Assert.That(jsonString, Does.Contain("3661"));
        Assert.That(jsonString, Does.Contain("1.00 Мб"));
        Assert.That(jsonString, Does.Contain("01:01:01"));
    }

    [Test]
    public void ToString_HandlesSpecialCharacters()
    {
        // Arrange
        var video = CreateTestVideo(
            title: "Тестовое видео с русскими символами \"и кавычками\"",
            filename: "test file & symbols.mp4"
        );
        var viewItem = new VideoViewItem(video);

        // Act
        var jsonString = viewItem.ToString();

        // Assert
        Assert.DoesNotThrow(() => JsonSerializer.Deserialize<object>(jsonString));
        Assert.That(jsonString, Does.Contain("Тестовое видео"));
    }

    [Test]
    public void ToString_UsesCorrectJsonOptions()
    {
        // Arrange
        var video = CreateTestVideo();
        var viewItem = new VideoViewItem(video);

        // Act
        var jsonString = viewItem.ToString();

        // Assert
        // JOptions.DefaultOptions has WriteIndented = true
        Assert.That(jsonString, Does.Contain("\n")); // Should be indented/multiline
    }

    #endregion

    #region Edge Cases and Validation Tests

    [Test]
    public void Constructor_WithEmptyStrings_HandlesCorrectly()
    {
        // Arrange
        var video = CreateTestVideo(
            title: "",
            filename: "",
            originalFilename: ""
        );

        // Act
        var viewItem = new VideoViewItem(video);

        // Assert
        Assert.That(viewItem.Title, Is.EqualTo(""));
        Assert.That(viewItem.Filename, Is.EqualTo(""));
        Assert.That(viewItem.OriginalFilename, Is.EqualTo(""));
    }

    [Test]
    public void Constructor_WithVeryLongStrings_HandlesCorrectly()
    {
        // Arrange
        var longString = new string('A', 1000);
        var video = CreateTestVideo(
            title: longString,
            filename: longString + ".mp4",
            originalFilename: longString + "_original.mp4"
        );

        // Act
        var viewItem = new VideoViewItem(video);

        // Assert
        Assert.That(viewItem.Title, Is.EqualTo(longString));
        Assert.That(viewItem.Filename, Has.Length.GreaterThan(1000));
        Assert.That(viewItem.OriginalFilename, Has.Length.GreaterThan(1000));
    }

    [Test]
    public void FileSize_PrecisionTest_ShowsCorrectDecimalPlaces()
    {
        // Test that megabytes and gigabytes show 2 decimal places
        var testCases = new[]
        {
            (1572864u, "1.50"), // 1.5 MB - should show .50
            (1610612736u, "1.50"), // 1.5 GB - should show .50
            (1073741824u, "1.00"), // 1 GB exactly - should show .00
        };

        foreach (var (size, expectedDecimal) in testCases)
        {
            var video = CreateTestVideo(fileSizeBytes: size);
            var viewItem = new VideoViewItem(video);
            
            Assert.That(viewItem.FileSize, Does.Contain(expectedDecimal), 
                $"Size {size} should contain decimal '{expectedDecimal}'");
        }
    }

    [Test]
    public void Duration_BoundaryValues_HandlesCorrectly()
    {
        // Test boundary values that might cause issues
        var boundaryValues = new uint[] 
        { 
            0, 1, 59, 60, 61, 3599, 3600, 3601,
            86399, 86400, 86401,
            uint.MaxValue 
        };

        foreach (var duration in boundaryValues)
        {
            var video = CreateTestVideo(durationSeconds: duration);
            var viewItem = new VideoViewItem(video);
            
            Assert.That(viewItem.Duration, Is.Not.Null);
            Assert.That(viewItem.Duration, Is.Not.Empty);
            Assert.That(viewItem.Duration, Does.Match(@"\d+:\d{2}:\d{2}"), 
                $"Duration {duration} should produce valid HH:mm:ss format");
        }
    }

    #endregion

    #region Performance Tests

    [Test]
    public void Constructor_WithManyInstances_PerformsWell()
    {
        // Arrange
        var videos = new List<Video>();
        for (int i = 0; i < 1000; i++)
        {
            videos.Add(CreateTestVideo(
                id: i,
                title: $"Video {i}",
                fileSizeBytes: (uint)(i * 1024),
                durationSeconds: (uint)(i * 60)
            ));
        }

        // Act
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var viewItems = videos.Select(v => new VideoViewItem(v)).ToList();
        stopwatch.Stop();

        // Assert
        Assert.That(viewItems, Has.Count.EqualTo(1000));
        Assert.That(stopwatch.ElapsedMilliseconds, Is.LessThan(100)); // Should be very fast
    }

    #endregion

    #region Helper Methods

    private static Video CreateTestVideo(
        int id = 1,
        string title = "Test Video",
        string filename = "test.mp4",
        string originalFilename = "original_test.mp4",
        uint fileSizeBytes = 1024,
        uint? durationSeconds = 60,
        int accountId = 1)
    {
        return new Video
        {
            Id = id,
            Title = title,
            Filename = filename,
            OriginalFilename = originalFilename,
            FileSizeBytes = fileSizeBytes,
            DurationSeconds = durationSeconds,
            AccountId = accountId
        };
    }

    #endregion

    #region Integration tests

    [Test]
    public void VideoViewItem_RealWorldScenario_FormatsCorrectly()
    {
        // Arrange - Real world video scenarios
        var scenarios = new[]
        {
            new {
                Name = "Small video clip",
                Size = 2_457_600u,      // ~2.3 MB
                Duration = 45u,         // 45 seconds
                ExpectedSize = "2.34 Мб",
                ExpectedDuration = "00:00:45"
            },
            new {
                Name = "Movie file",
                Size = 1_610_612_736u,  // 1.5 GB
                Duration = 7_200u,      // 2 hours
                ExpectedSize = "1.50 Гб",
                ExpectedDuration = "02:00:00"
            },
            new {
                Name = "High quality documentary",
                Size = 4_000_000_000u,  // ~3.7 GB
                Duration = 5_400u,      // 1.5 hours
                ExpectedSize = "3.73 Гб",
                ExpectedDuration = "01:30:00"
            },
            new {
                Name = "Short TikTok style video",
                Size = 512_000u,        // 500 KB
                Duration = 15u,         // 15 seconds
                ExpectedSize = "500 Кб",
                ExpectedDuration = "00:00:15"
            }
        };

        foreach (var scenario in scenarios)
        {
            // Arrange
            var video = new Video
            {
                Id = 1,
                Title = scenario.Name,
                Filename = "test.mp4",
                OriginalFilename = "original.mp4",
                FileSizeBytes = scenario.Size,
                DurationSeconds = scenario.Duration,
                AccountId = 1
            };

            // Act
            var viewItem = new VideoViewItem(video);

            // Assert
            Assert.That(viewItem.FileSize, Is.EqualTo(scenario.ExpectedSize),
                $"File size formatting failed for {scenario.Name}");
            Assert.That(viewItem.Duration, Is.EqualTo(scenario.ExpectedDuration),
                $"Duration formatting failed for {scenario.Name}");
        }
    }

    [Test]
    public void VideoViewItem_UnknownDuration_DisplaysCorrectMessage()
    {
        // Arrange - Video with unknown duration (common in streaming or corrupted files)
        var video = new Video
        {
            Id = 1,
            Title = "Video with unknown duration",
            Filename = "stream.mp4",
            OriginalFilename = "livestream.mp4",
            FileSizeBytes = 1_048_576, // 1 MB
            DurationSeconds = null,    // Unknown duration
            AccountId = 1
        };

        // Act
        var viewItem = new VideoViewItem(video);

        // Assert
        Assert.That(viewItem.FileSize, Is.EqualTo("1.00 Мб"));
        Assert.That(viewItem.Duration, Is.EqualTo("не известно"));

        // Verify JSON serialization includes both formatted strings
        var json = viewItem.ToString();
        Assert.That(json, Does.Contain("1.00 Мб"));
        Assert.That(json, Does.Contain("не известно"));
    }

    [Test]
    public void VideoViewItem_VerifyRussianLocalization()
    {
        // This test verifies that all Russian text is correctly encoded and displayed
        var testCases = new[]
        {
            (size: 0u, duration: (uint?)null, expectedSize: "0 байт", expectedDuration: "не известно"),
            (size: 1023u, duration: (uint?)1, expectedSize: "1023 байт", expectedDuration: "00:00:01"),
            (size: 1024u, duration: (uint?)60, expectedSize: "1 Кб", expectedDuration: "00:01:00"),
            (size: 1048576u, duration: (uint?)3600, expectedSize: "1.00 Мб", expectedDuration: "01:00:00"),
            (size: 1073741824u, duration: (uint?)86400, expectedSize: "1.00 Гб", expectedDuration: "24:00:00")
        };

        foreach (var (size, duration, expectedSize, expectedDuration) in testCases)
        {
            var video = new Video
            {
                Id = 1,
                Title = "Test",
                Filename = "test.mp4",
                OriginalFilename = "test.mp4",
                FileSizeBytes = size,
                DurationSeconds = duration,
                AccountId = 1
            };

            var viewItem = new VideoViewItem(video);

            Assert.That(viewItem.FileSize, Is.EqualTo(expectedSize));
            Assert.That(viewItem.Duration, Is.EqualTo(expectedDuration));
        }
    }
    #endregio
}
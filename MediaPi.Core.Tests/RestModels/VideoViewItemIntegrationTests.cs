// Copyright (c) 2025 sw.consulting
// This file is a part of Media Pi backend

using MediaPi.Core.Models;
using MediaPi.Core.RestModels;
using NUnit.Framework;

namespace MediaPi.Core.Tests.RestModels;

[TestFixture]
public class VideoViewItemIntegrationTests
{
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
}
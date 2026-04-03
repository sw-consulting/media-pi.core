// Copyright (c) 2025 sw.consulting
// This file is a part of Media Pi backend

using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace MediaPi.Core.Tests.TestHelpers;

/// <summary>
/// Helper for creating test video files with various scenarios
/// </summary>
public static class TestVideoFileGenerator
{
    /// <summary>
    /// Creates a simple test file with specified extension
    /// </summary>
    public static async Task<string> CreateTestFileWithExtensionAsync(string extension, string content = "test content")
    {
        var tempFile = Path.GetTempFileName();
        var targetFile = Path.ChangeExtension(tempFile, extension);
        
        await File.WriteAllTextAsync(targetFile, content);
        
        if (File.Exists(tempFile))
        {
            File.Delete(tempFile);
        }
        
        return targetFile;
    }

    /// <summary>
    /// Creates an empty file for testing zero-size scenarios
    /// </summary>
    public static async Task<string> CreateEmptyFileAsync(string? customPath = null)
    {
        var filePath = customPath ?? Path.GetTempFileName();
        await using var fs = File.Create(filePath);
        return filePath;
    }

    /// <summary>
    /// Creates a file with specific size for testing
    /// </summary>
    public static async Task<string> CreateTestFileWithSizeAsync(string extension, int sizeBytes)
    {
        var tempFile = Path.GetTempFileName();
        var targetFile = Path.ChangeExtension(tempFile, extension);
        
        var content = new byte[sizeBytes];
        // Fill with some pattern to make it more realistic
        for (int i = 0; i < sizeBytes; i++)
        {
            content[i] = (byte)(i % 256);
        }
        
        await File.WriteAllBytesAsync(targetFile, content);
        
        if (File.Exists(tempFile))
        {
            File.Delete(tempFile);
        }
        
        return targetFile;
    }
}

/// <summary>
/// Helper for creating real (playable) video files using ffmpeg
/// </summary>
public static class RealVideoFileGenerator
{
    /// <summary>
    /// Creates a real video file using ffmpeg. Returns null if ffmpeg is not available.
    /// </summary>
    public static async Task<string?> TryCreateRealVideoFileAsync(double durationSeconds = 1.0)
    {
        var targetFile = Path.Combine(Path.GetTempPath(), $"real_video_{Guid.NewGuid()}.mp4");
        try
        {
            var processInfo = new ProcessStartInfo
            {
                FileName = "ffmpeg",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            processInfo.ArgumentList.Add("-f");
            processInfo.ArgumentList.Add("lavfi");
            processInfo.ArgumentList.Add("-i");
            processInfo.ArgumentList.Add($"sine=frequency=440:duration={durationSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
            processInfo.ArgumentList.Add("-f");
            processInfo.ArgumentList.Add("lavfi");
            processInfo.ArgumentList.Add("-i");
            processInfo.ArgumentList.Add($"color=red:size=160x120:duration={durationSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
            processInfo.ArgumentList.Add("-y");
            processInfo.ArgumentList.Add(targetFile);

            using var process = new Process { StartInfo = processInfo };
            process.Start();
            await process.WaitForExitAsync();

            if (process.ExitCode == 0 && File.Exists(targetFile))
                return targetFile;

            // ffmpeg ran but didn't produce the expected file
            if (File.Exists(targetFile))
                File.Delete(targetFile);
            return null;
        }
        catch
        {
            // ffmpeg not available or failed unexpectedly
            if (File.Exists(targetFile))
                File.Delete(targetFile);
            return null;
        }
    }
}

/// <summary>
/// Helper for testing process execution scenarios
/// </summary>
public static class ProcessTestHelper
{
    /// <summary>
    /// Creates a mock process result for MediaInfo command
    /// </summary>
    public static (string StandardOutput, string StandardError, int ExitCode) CreateMediaInfoResult(
        string? duration = null, 
        bool success = true, 
        string? errorMessage = null)
    {
        if (!success)
        {
            return ("", errorMessage ?? "MediaInfo error", 1);
        }

        return (duration ?? "", "", 0);
    }

    /// <summary>
    /// Simulates various MediaInfo output formats
    /// </summary>
    public static class MediaInfoOutputs
    {
        public const string ValidDuration_10Seconds = "00:00:10.000";
        public const string ValidDuration_1Minute30Seconds = "00:01:30.500";
        public const string ValidDuration_1Hour = "01:00:00.000";
        public const string ValidDuration_DecimalSeconds = "123.456";
        public const string EmptyOutput = "";
        public const string InvalidFormat = "invalid_format";
        public const string? NullOutput = null;
    }
}
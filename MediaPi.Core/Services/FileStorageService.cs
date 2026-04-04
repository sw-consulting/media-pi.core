// Copyright (c) 2025-2026 sw.consulting
// This file is a part of Media Pi backend

using System.Globalization;
using Microsoft.Extensions.Options;

using MediaPi.Core.Services.Interfaces;
using MediaPi.Core.Settings;

namespace MediaPi.Core.Services;

public class FileStorageService : IFileStorageService
{
    private readonly VideoStorageSettings _settings;
    private readonly string _rootFullPath;
    private readonly string _rootFullPathWithSeparator;

    public FileStorageService(IOptions<VideoStorageSettings> options)
    {
        _settings = options.Value ?? throw new ArgumentNullException(nameof(options));

        if (string.IsNullOrWhiteSpace(_settings.RootPath))
        {
            throw new ArgumentException("RootPath must be provided", nameof(options));
        }

        _rootFullPath = Path.GetFullPath(_settings.RootPath);
        _rootFullPathWithSeparator = _rootFullPath.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
            ? _rootFullPath
            : _rootFullPath + Path.DirectorySeparatorChar;
        Directory.CreateDirectory(_rootFullPath);
    }

    public virtual async Task<FileSaveResult> SaveFileAsync(IFormFile file, string title, CancellationToken ct = default)
    {
        if (file.Length == 0) throw new ArgumentException("File is empty", nameof(file));

        if (file.Length > uint.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(file),
                $"File size {file.Length} bytes exceeds maximum supported size of {uint.MaxValue} bytes (4GB)");
        }

        var extension = Path.GetExtension(file.FileName);
        if (string.IsNullOrWhiteSpace(extension))
        {
            extension = ".bin";
        }

        var sanitizedTitle = SanitizeTitle(title);
        var uniqueName = $"{sanitizedTitle}-{Guid.NewGuid():N}{extension}";

        var targetDirectory = GetOrCreateTargetDirectory();
        var filePath = Path.Combine(targetDirectory, uniqueName);

        await using (var fs = new FileStream(filePath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            await file.CopyToAsync(fs, ct);
            await fs.FlushAsync(ct);
        }

        var relative = NormalizeRelativePath(Path.GetRelativePath(_rootFullPath, filePath));

        return new FileSaveResult
        {
            Filename = relative,
            OriginalFilename = file.FileName,
            FileSizeBytes = (uint)file.Length
        };
    }

    public virtual Task DeleteFileAsync(string storedFilename, CancellationToken ct = default)
    {
        var fullPath = GetAbsolutePath(storedFilename);
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }

        return Task.CompletedTask;
    }

    public string GetAbsolutePath(string storedFilename)
    {
        if (string.IsNullOrWhiteSpace(storedFilename)) throw new ArgumentException("Filename must be provided", nameof(storedFilename));

        var combined = Path.Combine(_rootFullPath, storedFilename);
        var fullPath = Path.GetFullPath(combined);
        var isInsideRoot = string.Equals(fullPath, _rootFullPath, StringComparison.Ordinal)
            || fullPath.StartsWith(_rootFullPathWithSeparator, StringComparison.Ordinal);
        if (!isInsideRoot)
        {
            throw new InvalidOperationException("Attempted to access a file outside of the storage root");
        }

        return fullPath;
    }

    protected string GetOrCreateTargetDirectory()
    {
        var directories = Directory.GetDirectories(_rootFullPath)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var directory in directories)
        {
            var fileCount = Directory.GetFiles(directory).Length;
            if (fileCount < _settings.MaxFilesPerDirectory)
            {
                return directory;
            }
        }

        var nextIndex = directories
            .Select(Path.GetFileName)
            .Select(name => int.TryParse(name, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : (int?)null)
            .Where(value => value.HasValue)
            .Select(value => value.GetValueOrDefault())
            .DefaultIfEmpty(0)
            .Max() + 1;

        var newDirectoryName = nextIndex.ToString("D4", CultureInfo.InvariantCulture);
        var newDirectoryPath = Path.Combine(_rootFullPath, newDirectoryName);
        Directory.CreateDirectory(newDirectoryPath);
        return newDirectoryPath;
    }

    protected static string NormalizeRelativePath(string relative)
    {
        return relative.Replace('\\', '/');
    }

    protected virtual string DefaultTitleToken => "file";

    protected string SanitizeTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return DefaultTitleToken;
        }

        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitizedChars = title
            .Trim()
            .ToLowerInvariant()
            .Select(ch => invalidChars.Contains(ch) ? '-' : ch)
            .Select(ch => char.IsWhiteSpace(ch) ? '-' : ch)
            .ToArray();

        var sanitized = new string(sanitizedChars);
        sanitized = string.Join('-', sanitized
            .Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

        return string.IsNullOrWhiteSpace(sanitized) ? DefaultTitleToken : sanitized;
    }
}

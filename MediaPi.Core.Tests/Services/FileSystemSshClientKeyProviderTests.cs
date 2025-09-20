// Copyright (c) 2025 sw.consulting
// This file is a part of Media Pi backend

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MediaPi.Core.Services;
using MediaPi.Core.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NUnit.Framework;

namespace MediaPi.Core.Tests.Services;

public class FileSystemSshClientKeyProviderTests
{
    [Test]
    public void GetPublicKey_WhenPathNotConfigured_ReturnsEmptyStringAndLogsWarning()
    {
        var logger = new TestLogger<FileSystemSshClientKeyProvider>();
        var provider = CreateProvider(new SshClientKeySettings
        {
            PublicKeyPath = "  ",
            PrivateKeyPath = "/keys/id_rsa",
        }, logger);

        var publicKey = provider.GetPublicKey();

        Assert.That(publicKey, Is.Empty);
        Assert.That(logger.Entries.Any(entry => entry.Level == LogLevel.Warning
            && entry.Message.Contains("not configured", StringComparison.OrdinalIgnoreCase)), Is.True);
    }

    [Test]
    public void GetPublicKey_WhenFileIsMissing_ReturnsEmptyStringAndLogsWarning()
    {
        var logger = new TestLogger<FileSystemSshClientKeyProvider>();
        var missingPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "id_rsa.pub");
        var provider = CreateProvider(new SshClientKeySettings
        {
            PublicKeyPath = missingPath,
            PrivateKeyPath = "/keys/id_rsa",
        }, logger);

        var publicKey = provider.GetPublicKey();

        Assert.That(publicKey, Is.Empty);
        Assert.That(logger.Entries.Any(entry => entry.Level == LogLevel.Warning
            && entry.Message.Contains("not found", StringComparison.OrdinalIgnoreCase)), Is.True);
    }

    [Test]
    public void GetPublicKey_WhenReadFails_LogsErrorAndReturnsEmptyString()
    {
        var logger = new TestLogger<FileSystemSshClientKeyProvider>();
        var directory = CreateTempDirectory();
        try
        {
            var path = Path.Combine(directory, "id_rsa.pub");
            File.WriteAllText(path, "ssh-rsa AAAAB3NzaC1yc2EAAAADAQABAAABAQC7");
            var provider = CreateProvider(new SshClientKeySettings
            {
                PublicKeyPath = path,
                PrivateKeyPath = "/keys/id_rsa",
            }, logger);

            using var fileLock = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None);
            var publicKey = provider.GetPublicKey();

            Assert.That(publicKey, Is.Empty);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }

        var errorEntry = logger.Entries.Single(entry => entry.Level == LogLevel.Error);
        Assert.That(errorEntry.Message, Does.Contain("Failed to read SSH public key"));
        Assert.That(errorEntry.Exception, Is.Not.Null);
    }

    [Test]
    public void GetPublicKey_WhenFileExists_ReturnsTrimmedContentAndCachesResult()
    {
        var logger = new TestLogger<FileSystemSshClientKeyProvider>();
        var directory = CreateTempDirectory();
        try
        {
            var path = Path.Combine(directory, "id_rsa.pub");
            File.WriteAllText(path, "ssh-rsa AAAAB3NzaC1yc2EAAAADAQABAAABAQD7  \n");
            var provider = CreateProvider(new SshClientKeySettings
            {
                PublicKeyPath = path,
                PrivateKeyPath = "/keys/id_rsa",
            }, logger);

            var firstRead = provider.GetPublicKey();
            Assert.That(firstRead, Is.EqualTo("ssh-rsa AAAAB3NzaC1yc2EAAAADAQABAAABAQD7"));
            Assert.That(provider.GetPrivateKeyPath(), Is.EqualTo("/keys/id_rsa"));

            File.WriteAllText(path, "should not be read");
            var secondRead = provider.GetPublicKey();

            Assert.That(secondRead, Is.EqualTo(firstRead));
            Assert.That(logger.Entries, Is.Empty);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    private static FileSystemSshClientKeyProvider CreateProvider(
        SshClientKeySettings settings,
        ILogger<FileSystemSshClientKeyProvider> logger) =>
        new(Options.Create(settings), logger);

    private static string CreateTempDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "mediapi-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private sealed class TestLogger<T> : ILogger<T>
    {
        private readonly List<LogEntry> _entries = new();

        public IReadOnlyList<LogEntry> Entries => _entries;

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (formatter is null)
            {
                throw new ArgumentNullException(nameof(formatter));
            }

            var message = formatter(state, exception);
            _entries.Add(new LogEntry(logLevel, message, exception));
        }

        public readonly record struct LogEntry(LogLevel Level, string Message, Exception? Exception);

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();

            public void Dispose()
            {
            }
        }
    }
}

// Copyright (c) 2025 sw.consulting
// This file is a part of Media Pi backend

using MediaPi.Core.Services.Interfaces;
using MediaPi.Core.Settings;
using Microsoft.Extensions.Options;

namespace MediaPi.Core.Services;

/// <summary>
/// Provides access to an SSH client keypair stored on the server file system.
/// Only the public key content is exposed (for devices to trust). The private
/// key path is returned only for internal usage (e.g. when initiating SSH
/// sessions). The private key itself is not loaded into memory here to avoid
/// accidental exposure in logs/dumps.
/// </summary>
public sealed class FileSystemSshClientKeyProvider : ISshClientKeyProvider
{
    private readonly SshClientKeySettings _settings;
    private readonly Lazy<string> _lazyPublicKey;
    private readonly ILogger<FileSystemSshClientKeyProvider> _logger;

    public FileSystemSshClientKeyProvider(IOptions<SshClientKeySettings> options, ILogger<FileSystemSshClientKeyProvider> logger)
    {
        _settings = options.Value;
        _logger = logger;
        _lazyPublicKey = new Lazy<string>(LoadPublicKey, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public string GetPublicKey() => _lazyPublicKey.Value;

    public string GetPrivateKeyPath() => _settings.PrivateKeyPath;

    private string LoadPublicKey()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(_settings.PublicKeyPath))
            {
                _logger.LogWarning("SSH public key path not configured");
                return string.Empty;
            }
            if (!File.Exists(_settings.PublicKeyPath))
            {
                _logger.LogWarning("SSH public key file not found at {path}", _settings.PublicKeyPath);
                return string.Empty;
            }
            return File.ReadAllText(_settings.PublicKeyPath).Trim();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read SSH public key");
            return string.Empty;
        }
    }
}

// MIT License
//
// Copyright (c) 2025 Maxim [maxirmx] Samsonov (www.sw.consulting)
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

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

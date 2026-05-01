// Copyright (C) 2025-2026 sw.consulting
// This file is a part of Media Pi backend

using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Linq;
using Microsoft.AspNetCore.StaticFiles;
using MediaPi.Core.Models;
using MediaPi.Core.Services.Interfaces;
using MediaPi.Core.Services.Models;

namespace MediaPi.Core.Services;

public sealed class DeviceAgentRestClient : IMediaPiAgentClient
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly HttpClient _httpClient;
    private readonly ILogger<DeviceAgentRestClient> _logger;
    private static readonly FileExtensionContentTypeProvider ContentTypeProvider = new();

    public DeviceAgentRestClient(HttpClient httpClient, ILogger<DeviceAgentRestClient> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<MediaPiAgentListResponse> ListUnitsAsync(Device device, CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(device, HttpMethod.Get, "/api/units");
        var result = await SendAsync<List<DeviceUnitInfo>>(request, cancellationToken).ConfigureAwait(false);

        var units = result.Response.Data?.Select(u => new MediaPiAgentListUnit
        {
            Unit = u.Unit,
            Active = u.Active,
            Sub = u.Sub,
            Error = u.Error
        }).ToArray() ?? Array.Empty<MediaPiAgentListUnit>();

        return new MediaPiAgentListResponse
        {
            Ok = result.Response.Ok,
            ErrMsg = NormalizeError(result),
            Units = units
        };
    }

    public async Task<MediaPiAgentStatusResponse> GetStatusAsync(Device device, string unit, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(unit))
        {
            throw new ArgumentException("Unit name must be provided.", nameof(unit));
        }

        var query = $"unit={Uri.EscapeDataString(unit.Trim())}";
        using var request = CreateRequest(device, HttpMethod.Get, "/api/units/status", query);
        var result = await SendAsync<DeviceUnitInfo>(request, cancellationToken).ConfigureAwait(false);
        var data = result.Response.Data;

        return new MediaPiAgentStatusResponse
        {
            Ok = result.Response.Ok,
            ErrMsg = NormalizeError(result),
            Unit = data?.Unit ?? unit,
            Active = data?.Active ?? default,
            Sub = data?.Sub ?? default
        };
    }

    public async Task<MediaPiAgentHealthResponse> CheckHealthAsync(Device device, CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(device, HttpMethod.Get, "/health");
        var result = await SendAsync<HealthInfo>(request, cancellationToken).ConfigureAwait(false);
        var data = result.Response.Data;

        return new MediaPiAgentHealthResponse
        {
            Ok = result.Response.Ok,
            ErrMsg = NormalizeError(result),
            Status = data?.Status,
            Uptime = data?.Uptime,
            Version = data?.Version
        };
    }

    // Maximum permitted size for a screenshot response body (20 MB).
    internal const int MaxScreenshotBytes = 20 * 1024 * 1024;

    // 80 KB read buffer – matches the default buffer size used by Stream.CopyToAsync.
    private const int StreamBufferSize = 80 * 1024;

    public async Task<DeviceScreenshotResult> CreateScreenshotAsync(Device device, CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(device, HttpMethod.Get, "/api/menu/screenshot/take");
        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Screenshot endpoint returned status {(int)response.StatusCode} ({response.StatusCode}).");
        }

        var contentLength = response.Content.Headers.ContentLength;
        if (contentLength.HasValue && contentLength.Value > MaxScreenshotBytes)
        {
            throw new InvalidOperationException($"Screenshot response body ({contentLength.Value} bytes) exceeds the maximum allowed size of {MaxScreenshotBytes / (1024 * 1024)} MB.");
        }

        byte[] content;
        await using (var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false))
        await using (var buffer = new MemoryStream())
        {
            var chunk = new byte[StreamBufferSize];
            int bytesRead;
            while ((bytesRead = await responseStream.ReadAsync(chunk, cancellationToken).ConfigureAwait(false)) > 0)
            {
                if (buffer.Length + bytesRead > MaxScreenshotBytes)
                {
                    throw new InvalidOperationException($"Screenshot response body exceeds the maximum allowed size of {MaxScreenshotBytes / (1024 * 1024)} MB.");
                }
                buffer.Write(chunk, 0, bytesRead);
            }
            content = buffer.ToArray();
        }

        if (content.Length == 0)
        {
            throw new InvalidOperationException("Screenshot endpoint returned an empty response body.");
        }

        var rawContentType = response.Content.Headers.ContentType?.MediaType;
        var contentType = (!string.IsNullOrWhiteSpace(rawContentType) && rawContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            ? rawContentType
            : "image/jpeg";

        var filename = ResolveScreenshotFilename(response, contentType);
        return new DeviceScreenshotResult
        {
            Content = content,
            ContentType = contentType,
            Filename = filename
        };
    }

    public Task<MediaPiAgentUnitResultResponse> StartUnitAsync(Device device, string unit, CancellationToken cancellationToken = default) =>
        ExecuteUnitCommandAsync(device, unit, "/api/units/start", cancellationToken);

    public Task<MediaPiAgentUnitResultResponse> StopUnitAsync(Device device, string unit, CancellationToken cancellationToken = default) =>
        ExecuteUnitCommandAsync(device, unit, "/api/units/stop", cancellationToken);

    public Task<MediaPiAgentUnitResultResponse> RestartUnitAsync(Device device, string unit, CancellationToken cancellationToken = default) =>
        ExecuteUnitCommandAsync(device, unit, "/api/units/restart", cancellationToken);

    public Task<MediaPiAgentEnableResponse> EnableUnitAsync(Device device, string unit, CancellationToken cancellationToken = default) =>
        ExecuteEnableCommandAsync(device, unit, "/api/units/enable", cancellationToken);

    public Task<MediaPiAgentEnableResponse> DisableUnitAsync(Device device, string unit, CancellationToken cancellationToken = default) =>
        ExecuteEnableCommandAsync(device, unit, "/api/units/disable", cancellationToken);

    private async Task<MediaPiAgentUnitResultResponse> ExecuteUnitCommandAsync(Device device, string unit, string path, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(unit))
        {
            throw new ArgumentException("Unit name must be provided.", nameof(unit));
        }

        using var request = CreateRequest(device, HttpMethod.Post, path, content: CreateJsonContent(new UnitActionRequest
        {
            Unit = unit.Trim()
        }));

        var result = await SendAsync<UnitActionResponse>(request, cancellationToken).ConfigureAwait(false);
        var data = result.Response.Data;

        return new MediaPiAgentUnitResultResponse
        {
            Ok = result.Response.Ok,
            ErrMsg = NormalizeError(result),
            Unit = data?.Unit ?? unit,
            Result = data?.Result
        };
    }

    private async Task<MediaPiAgentEnableResponse> ExecuteEnableCommandAsync(Device device, string unit, string path, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(unit))
        {
            throw new ArgumentException("Unit name must be provided.", nameof(unit));
        }

        using var request = CreateRequest(device, HttpMethod.Post, path, content: CreateJsonContent(new UnitActionRequest
        {
            Unit = unit.Trim()
        }));

        var result = await SendAsync<UnitActionResponse>(request, cancellationToken).ConfigureAwait(false);
        var data = result.Response.Data;
        var enabled = string.Equals(data?.Result, "enabled", StringComparison.OrdinalIgnoreCase);

        return new MediaPiAgentEnableResponse
        {
            Ok = result.Response.Ok,
            ErrMsg = NormalizeError(result),
            Unit = data?.Unit ?? unit,
            Enabled = enabled && result.Response.Ok,
            Result = data?.Result
        };
    }

    private HttpRequestMessage CreateRequest(Device device, HttpMethod method, string path, string? query = null, HttpContent? content = null)
    {
        if (device is null)
        {
            throw new ArgumentNullException(nameof(device));
        }

        if (string.IsNullOrWhiteSpace(device.IpAddress))
        {
            throw new ArgumentException("Device IP address must be provided.", nameof(device));
        }

        var uri = BuildUri(device, path, query);
        var request = new HttpRequestMessage(method, uri);

        if (!string.IsNullOrWhiteSpace(device.ServerKey))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", device.ServerKey.Trim());
        }
        else
        {
            _logger.LogWarning("Device {DeviceId} does not have a server key configured.", device.Id);
        }

        if (content != null)
        {
            request.Content = content;
        }

        return request;
    }

    private async Task<DeviceApiResult<T>> SendAsync<T>(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            var raw = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            DeviceApiResponse<T>? envelope = null;
            if (!string.IsNullOrWhiteSpace(raw))
            {
                try
                {
                    envelope = JsonSerializer.Deserialize<DeviceApiResponse<T>>(raw, SerializerOptions);
                }
                catch (JsonException ex)
                {
                    throw new InvalidOperationException($"Failed to deserialize response from device API at {request.RequestUri}.", ex);
                }
            }

            envelope ??= new DeviceApiResponse<T>();
            return new DeviceApiResult<T>(envelope, response.StatusCode);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"Request to device API at {request.RequestUri} timed out.", ex);
        }
    }

    private static HttpContent CreateJsonContent<T>(T payload)
    {
        var json = JsonSerializer.Serialize(payload, SerializerOptions);
        return new StringContent(json, Encoding.UTF8, "application/json");
    }

    private static string ResolveScreenshotFilename(HttpResponseMessage response, string contentType)
    {
        var fromHeader = response.Content.Headers.ContentDisposition?.FileNameStar
            ?? response.Content.Headers.ContentDisposition?.FileName;

        if (!string.IsNullOrWhiteSpace(fromHeader))
        {
            var safe = NormalizeSafeFilename(fromHeader.Trim('"'));
            if (!string.IsNullOrWhiteSpace(safe))
                return EnsureFilenameHasExtension(safe, contentType);
        }

        var extension = GetExtensionForContentType(contentType);
        return $"screenshot_{DateTime.UtcNow:yyyy-MM-dd_HH-mm-ss}{extension}";
    }

    private static string EnsureFilenameHasExtension(string filename, string contentType)
    {
        return string.IsNullOrEmpty(Path.GetExtension(filename))
            ? $"{filename}{GetExtensionForContentType(contentType)}"
            : filename;
    }

    private static string GetExtensionForContentType(string contentType)
    {
        if (string.Equals(contentType, "image/jpeg", StringComparison.OrdinalIgnoreCase))
            return ".jpg";
        if (string.Equals(contentType, "image/png", StringComparison.OrdinalIgnoreCase))
            return ".png";
        if (string.Equals(contentType, "image/gif", StringComparison.OrdinalIgnoreCase))
            return ".gif";
        if (string.Equals(contentType, "image/webp", StringComparison.OrdinalIgnoreCase))
            return ".webp";

        return ContentTypeProvider.Mappings
            .FirstOrDefault(m => string.Equals(m.Value, contentType, StringComparison.OrdinalIgnoreCase))
            .Key ?? ".jpg";
    }

    // Explicit chars ensure the set is safe cross-platform: Path.GetInvalidFileNameChars() on Linux
    // only returns '\0' and '/'; the extras (|, <, >, *, ?, ", :, \) cover Windows-invalid chars too.
    private static readonly HashSet<char> UnsafeFileNameChars = new(
        Path.GetInvalidFileNameChars().Concat(new[] { '\\', '/', ':', '*', '?', '"', '<', '>', '|' }));

    private const int MaxSafeFilenameLength = 200;

    private static string NormalizeSafeFilename(string input)
    {
        // Replace backslashes so Path.GetFileName also strips Windows-style path segments
        var name = Path.GetFileName(input.Replace('\\', '/'));
        var result = new System.Text.StringBuilder(name.Length);
        foreach (var c in name)
        {
            if (char.IsControl(c) || UnsafeFileNameChars.Contains(c))
                result.Append('_');
            else
                result.Append(c);
        }
        var normalized = result.ToString().Trim();
        if (normalized.Length <= MaxSafeFilenameLength)
            return normalized;

        // Preserve the file extension when trimming to the length limit
        var ext = Path.GetExtension(normalized);
        var maxBase = MaxSafeFilenameLength - ext.Length;
        return maxBase > 0
            ? normalized[..maxBase] + ext
            : normalized[..MaxSafeFilenameLength];
    }

    private Uri BuildUri(Device device, string path, string? query)
    {
        if (!IPAddress.TryParse(device.IpAddress, out var ipAddress))
        {
            throw new InvalidOperationException($"Invalid IP address '{device.IpAddress}' for device {device.Id}.");
        }

        var port = ResolvePort(device);
        var builder = new UriBuilder
        {
            Scheme = "http",
            Host = ipAddress.ToString(),
            Port = port,
            Path = path,
            Query = query ?? string.Empty
        };
        return builder.Uri;
    }

    private int ResolvePort(Device device)
    {
        var port = device.Port;
        if (port > 0)
        {
            return port;
        }

        _logger.LogWarning("Device {DeviceId} has invalid port '{PortValue}'. Falling back to default 8081.", device.Id, device.Port);
        return 8081;
    }

    private static string? NormalizeError<T>(DeviceApiResult<T> result)
    {
        if (result.Response.Ok)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(result.Response.ErrMsg))
        {
            return result.Response.ErrMsg;
        }

        if (result.StatusCode != HttpStatusCode.OK)
        {
            return $"Device API responded with status {(int)result.StatusCode} ({result.StatusCode}).";
        }

        return "Device API returned an unsuccessful response without an error message.";
    }

    private sealed class DeviceApiResponse<T>
    {
        public bool Ok { get; init; }
        public string? ErrMsg { get; init; }
        public T? Data { get; init; }
    }

    private sealed record DeviceApiResult<T>(DeviceApiResponse<T> Response, HttpStatusCode StatusCode);

    private sealed class DeviceUnitInfo
    {
        public string? Unit { get; init; }
        public JsonElement Active { get; init; }
        public JsonElement Sub { get; init; }
        public string? Error { get; init; }
    }

    private sealed class HealthInfo
    {
        public string? Status { get; init; }
        public double? Uptime { get; init; }
        public string? Version { get; init; }
    }

    private sealed class UnitActionRequest
    {
        public string Unit { get; init; } = string.Empty;
    }

    private sealed class UnitActionResponse
    {
        public string? Unit { get; init; }
        public string? Result { get; init; }
    }
}

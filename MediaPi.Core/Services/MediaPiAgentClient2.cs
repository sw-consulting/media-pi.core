// Copyright (c) 2025 sw.consulting
// This file is a part of Media Pi backend

using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using MediaPi.Core.Models;
using MediaPi.Core.Services.Interfaces;
using MediaPi.Core.Services.Models;
using Microsoft.Extensions.Logging;

namespace MediaPi.Core.Services;

public sealed class MediaPiAgentClient2 : IMediaPiAgentClient2
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly HttpClient _httpClient;
    private readonly ILogger<MediaPiAgentClient2> _logger;

    public MediaPiAgentClient2(HttpClient httpClient, ILogger<MediaPiAgentClient2> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<MediaPiMenuCommandResponse> StopPlaybackAsync(Device device, CancellationToken cancellationToken = default) =>
        ExecuteCommandAsync(device, HttpMethod.Post, "/api/menu/playback/stop", cancellationToken);

    public Task<MediaPiMenuCommandResponse> StartPlaybackAsync(Device device, CancellationToken cancellationToken = default) =>
        ExecuteCommandAsync(device, HttpMethod.Post, "/api/menu/playback/start", cancellationToken);

    public Task<MediaPiMenuDataResponse> CheckStorageAsync(Device device, CancellationToken cancellationToken = default) =>
        ExecuteDataRequestAsync(device, HttpMethod.Get, "/api/menu/storage/check", cancellationToken);

    public Task<MediaPiMenuDataResponse> GetPlaylistSettingsAsync(Device device, CancellationToken cancellationToken = default) =>
        ExecuteDataRequestAsync(device, HttpMethod.Get, "/api/menu/playlist/get", cancellationToken);

    public Task<MediaPiMenuCommandResponse> UpdatePlaylistSettingsAsync<TPayload>(Device device, TPayload payload, CancellationToken cancellationToken = default) =>
        ExecuteCommandWithPayloadAsync(device, HttpMethod.Put, "/api/menu/playlist/update", payload, cancellationToken);

    public Task<MediaPiMenuCommandResponse> StartPlaylistUploadAsync(Device device, CancellationToken cancellationToken = default) =>
        ExecuteCommandAsync(device, HttpMethod.Post, "/api/menu/playlist/start-upload", cancellationToken);

    public Task<MediaPiMenuCommandResponse> StopPlaylistUploadAsync(Device device, CancellationToken cancellationToken = default) =>
        ExecuteCommandAsync(device, HttpMethod.Post, "/api/menu/playlist/stop-upload", cancellationToken);

    public Task<MediaPiMenuDataResponse> GetScheduleAsync(Device device, CancellationToken cancellationToken = default) =>
        ExecuteDataRequestAsync(device, HttpMethod.Get, "/api/menu/schedule/get", cancellationToken);

    public Task<MediaPiMenuCommandResponse> UpdateScheduleAsync<TPayload>(Device device, TPayload payload, CancellationToken cancellationToken = default) =>
        ExecuteCommandWithPayloadAsync(device, HttpMethod.Put, "/api/menu/schedule/update", payload, cancellationToken);

    public Task<MediaPiMenuDataResponse> GetAudioSettingsAsync(Device device, CancellationToken cancellationToken = default) =>
        ExecuteDataRequestAsync(device, HttpMethod.Get, "/api/menu/audio/get", cancellationToken);

    public Task<MediaPiMenuCommandResponse> UpdateAudioSettingsAsync<TPayload>(Device device, TPayload payload, CancellationToken cancellationToken = default) =>
        ExecuteCommandWithPayloadAsync(device, HttpMethod.Put, "/api/menu/audio/update", payload, cancellationToken);

    public Task<MediaPiMenuCommandResponse> ReloadSystemAsync(Device device, CancellationToken cancellationToken = default) =>
        ExecuteCommandAsync(device, HttpMethod.Post, "/api/menu/system/reload", cancellationToken);

    public Task<MediaPiMenuCommandResponse> RebootSystemAsync(Device device, CancellationToken cancellationToken = default) =>
        ExecuteCommandAsync(device, HttpMethod.Post, "/api/menu/system/reboot", cancellationToken);

    public Task<MediaPiMenuCommandResponse> ShutdownSystemAsync(Device device, CancellationToken cancellationToken = default) =>
        ExecuteCommandAsync(device, HttpMethod.Post, "/api/menu/system/shutdown", cancellationToken);

    private async Task<MediaPiMenuCommandResponse> ExecuteCommandAsync(Device device, HttpMethod method, string path, CancellationToken cancellationToken)
    {
        using var request = CreateRequest(device, method, path);
        return await SendCommandAsync(request, cancellationToken).ConfigureAwait(false);
    }

    private async Task<MediaPiMenuCommandResponse> ExecuteCommandWithPayloadAsync<TPayload>(Device device, HttpMethod method, string path, TPayload payload, CancellationToken cancellationToken)
    {
        if (payload is null)
        {
            throw new ArgumentNullException(nameof(payload));
        }

        using var request = CreateRequest(device, method, path, content: CreateJsonContent(payload));
        return await SendCommandAsync(request, cancellationToken).ConfigureAwait(false);
    }

    private async Task<MediaPiMenuDataResponse> ExecuteDataRequestAsync(Device device, HttpMethod method, string path, CancellationToken cancellationToken)
    {
        using var request = CreateRequest(device, method, path);
        return await SendDataAsync(request, cancellationToken).ConfigureAwait(false);
    }

    private HttpRequestMessage CreateRequest(Device device, HttpMethod method, string path, HttpContent? content = null)
    {
        if (device is null)
        {
            throw new ArgumentNullException(nameof(device));
        }

        if (string.IsNullOrWhiteSpace(device.IpAddress))
        {
            throw new ArgumentException("Device IP address must be provided.", nameof(device));
        }

        var uri = BuildUri(device, path);
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

    private async Task<MediaPiMenuCommandResponse> SendCommandAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var result = await SendAsync(request, cancellationToken).ConfigureAwait(false);
        var data = CloneElement(result.Response.Data);

        return new MediaPiMenuCommandResponse
        {
            Ok = result.Response.Ok,
            Error = NormalizeError(result),
            Data = data,
            Result = ExtractResult(data)
        };
    }

    private async Task<MediaPiMenuDataResponse> SendDataAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var result = await SendAsync(request, cancellationToken).ConfigureAwait(false);

        return new MediaPiMenuDataResponse
        {
            Ok = result.Response.Ok,
            Error = NormalizeError(result),
            Data = CloneElement(result.Response.Data)
        };
    }

    private async Task<DeviceMenuResult> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            var raw = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            DeviceMenuEnvelope? envelope = null;
            if (!string.IsNullOrWhiteSpace(raw))
            {
                try
                {
                    envelope = JsonSerializer.Deserialize<DeviceMenuEnvelope>(raw, SerializerOptions);
                }
                catch (JsonException ex)
                {
                    throw new InvalidOperationException($"Failed to deserialize response from device API at {request.RequestUri}.", ex);
                }
            }

            envelope ??= new DeviceMenuEnvelope();
            return new DeviceMenuResult(envelope, response.StatusCode);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"Request to device API at {request.RequestUri} timed out.", ex);
        }
    }

    private static JsonElement CloneElement(JsonElement element)
    {
        return element.ValueKind == JsonValueKind.Undefined ? default : element.Clone();
    }

    private static string? ExtractResult(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Object when element.TryGetProperty("result", out var resultElement) && resultElement.ValueKind == JsonValueKind.String
                => resultElement.GetString(),
            _ => null
        };
    }

    private static HttpContent CreateJsonContent<T>(T payload)
    {
        var json = JsonSerializer.Serialize(payload, SerializerOptions);
        return new StringContent(json, Encoding.UTF8, "application/json");
    }

    private Uri BuildUri(Device device, string path)
    {
        if (!IPAddress.TryParse(device.IpAddress, out var ipAddress))
        {
            throw new InvalidOperationException($"Invalid IP address '{device.IpAddress}' for device {device.Id}.");
        }

        var builder = new UriBuilder
        {
            Scheme = "http",
            Host = ipAddress.ToString(),
            Port = ResolvePort(device),
            Path = path
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

    private static string? NormalizeError(DeviceMenuResult result)
    {
        if (result.Response.Ok)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(result.Response.Error))
        {
            return result.Response.Error;
        }

        if (result.StatusCode != HttpStatusCode.OK)
        {
            return $"Device API responded with status {(int)result.StatusCode} ({result.StatusCode}).";
        }

        return "Device API returned an unsuccessful response without an error message.";
    }

    private sealed class DeviceMenuEnvelope
    {
        public bool Ok { get; init; }
        public string? Error { get; init; }
        public JsonElement Data { get; init; }
    }

    private sealed record DeviceMenuResult(DeviceMenuEnvelope Response, HttpStatusCode StatusCode);
}

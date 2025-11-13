// Copyright (c) 2025 sw.consulting
// This file is a part of Media Pi backend

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MediaPi.Core.Models;
using MediaPi.Core.Services;
using MediaPi.Core.Services.Models;
using Microsoft.Extensions.Logging;
using NUnit.Framework;

namespace MediaPi.Core.Tests.Services;

[TestFixture]
public class MediaPiAgentClient2Tests
{
    [Test]
    public void Constructor_WhenHttpClientIsNull_Throws()
    {
        var logger = new TestLogger<MediaPiAgentClient2>();

        Assert.Throws<ArgumentNullException>(() => _ = new MediaPiAgentClient2(null!, logger));
    }

    [Test]
    public void Constructor_WhenLoggerIsNull_Throws()
    {
        var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
        var httpClient = new HttpClient(handler);

        Assert.Throws<ArgumentNullException>(() => _ = new MediaPiAgentClient2(httpClient, null!));
    }

    [Test]
    public async Task PostCommands_SendPostToExpectedPaths()
    {
        var observed = new List<(HttpMethod Method, Uri Uri)>();
        var handler = new StubHttpMessageHandler((request, _) =>
        {
            observed.Add((request.Method, request.RequestUri!));
            var body = JsonSerializer.Serialize(new { ok = true, data = new { result = "done" } });
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
        });

        var logger = new TestLogger<MediaPiAgentClient2>();
        var client = CreateClient(handler, logger);
        var device = CreateDevice(port: 1200, serverKey: " key ");

        await client.StopPlaybackAsync(device, CancellationToken.None);
        await client.StartPlaybackAsync(device, CancellationToken.None);
        await client.StartPlaylistUploadAsync(device, CancellationToken.None);
        await client.StopPlaylistUploadAsync(device, CancellationToken.None);
        await client.ReloadSystemAsync(device, CancellationToken.None);
        await client.RebootSystemAsync(device, CancellationToken.None);
        await client.ShutdownSystemAsync(device, CancellationToken.None);

        Assert.That(observed.Select(o => o.Method), Is.All.EqualTo(HttpMethod.Post));
        Assert.That(observed.Select(o => o.Uri.AbsolutePath), Is.EqualTo(new[]
        {
            "/api/menu/playback/stop",
            "/api/menu/playback/start",
            "/api/menu/playlist/start-upload",
            "/api/menu/playlist/stop-upload",
            "/api/menu/system/reload",
            "/api/menu/system/reboot",
            "/api/menu/system/shutdown"
        }));
        Assert.That(observed.Select(o => o.Uri.Port), Is.All.EqualTo(1200));
        Assert.That(logger.Entries.Any(e => e.Level == LogLevel.Warning && e.Message.Contains("does not have a server key")), Is.False);
    }

    [Test]
    public async Task DataEndpoints_ReturnDataAndAllowDeserialize()
    {
        var handler = new StubHttpMessageHandler((request, _) =>
        {
            Assert.That(request.Method, Is.EqualTo(HttpMethod.Get));
            var json = JsonSerializer.Serialize(new { ok = true, data = new { channel = "HDMI", volume = 75 } });
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
        });

        var client = CreateClient(handler);
        var device = CreateDevice();

        var response = await client.GetAudioSettingsAsync(device, CancellationToken.None);

        Assert.That(response.Ok, Is.True);
        Assert.That(response.Error, Is.Null);
        Assert.That(response.HasData, Is.True);

        var model = response.DeserializeData<AudioSettings>();
        Assert.That(model, Is.Not.Null);
        Assert.That(model!.Channel, Is.EqualTo("HDMI"));
        Assert.That(model.Volume, Is.EqualTo(75));
    }

    [Test]
    public async Task UpdatePlaylistSettingsAsync_SendsPayload()
    {
        string? observedContent = null;
        AuthenticationHeaderValue? observedAuth = null;

        var handler = new StubHttpMessageHandler(async (request, cancellationToken) =>
        {
            observedContent = await request.Content!.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            observedAuth = request.Headers.Authorization;

            var json = JsonSerializer.Serialize(new { ok = true, data = new { result = "updated" } });
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
        });

        var client = CreateClient(handler);
        var device = CreateDevice(serverKey: "secret");
        var payload = new { enabled = true, login = "user" };

        var response = await client.UpdatePlaylistSettingsAsync(device, payload, CancellationToken.None);

        Assert.That(response.Result, Is.EqualTo("updated"));
        Assert.That(observedContent, Is.EqualTo("{\"enabled\":true,\"login\":\"user\"}"));
        Assert.That(observedAuth, Is.Not.Null);
        Assert.That(observedAuth!.Scheme, Is.EqualTo("Bearer"));
        Assert.That(observedAuth.Parameter, Is.EqualTo("secret"));
    }

    [Test]
    public void UpdateScheduleAsync_WhenPayloadIsNull_Throws()
    {
        var client = CreateClient(new StubHttpMessageHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK))));
        var device = CreateDevice();

        Assert.ThrowsAsync<ArgumentNullException>(() => client.UpdateScheduleAsync<object?>(device, null!, CancellationToken.None));
    }

    [Test]
    public async Task GetPlaylistSettingsAsync_WhenResponseEmpty_HasDataFalse()
    {
        var handler = new StubHttpMessageHandler((_, _) =>
        {
            var json = JsonSerializer.Serialize(new { ok = true });
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
        });

        var client = CreateClient(handler);
        var device = CreateDevice();

        var response = await client.GetPlaylistSettingsAsync(device, CancellationToken.None);

        Assert.That(response.Ok, Is.True);
        Assert.That(response.HasData, Is.False);
        JsonElement? empty = response.DeserializeData<JsonElement>();
        Assert.That(empty.GetValueOrDefault().ValueKind, Is.EqualTo(JsonValueKind.Undefined));
    }

    [Test]
    public async Task StopPlaybackAsync_WhenDeviceReturnsError_NormalizesMessage()
    {
        var handler = new StubHttpMessageHandler((_, _) =>
        {
            var json = JsonSerializer.Serialize(new { ok = false });
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
        });

        var client = CreateClient(handler);
        var device = CreateDevice();

        var response = await client.StopPlaybackAsync(device, CancellationToken.None);

        Assert.That(response.Ok, Is.False);
        Assert.That(response.Error, Is.EqualTo("Device API responded with status 400 (BadRequest)."));
    }

    [Test]
    public void StartPlaybackAsync_WhenDeviceIpInvalid_Throws()
    {
        var client = CreateClient(new StubHttpMessageHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK))));
        var device = CreateDevice(ip: "invalid");

        Assert.ThrowsAsync<InvalidOperationException>(() => client.StartPlaybackAsync(device, CancellationToken.None));
    }

    [Test]
    public void ReloadSystemAsync_WhenTimeoutOccurs_Throws()
    {
        var handler = new StubHttpMessageHandler((_, _) => throw new TaskCanceledException());
        var client = CreateClient(handler);
        var device = CreateDevice();

        Assert.ThrowsAsync<TimeoutException>(() => client.ReloadSystemAsync(device, CancellationToken.None));
    }

    [Test]
    public async Task GetScheduleAsync_WhenPortInvalid_LogsWarningAndUsesDefault()
    {
        Uri? observedUri = null;
        var handler = new StubHttpMessageHandler((request, _) =>
        {
            observedUri = request.RequestUri;
            var json = JsonSerializer.Serialize(new { ok = true, data = new { cron = "* * * * *" } });
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
        });

        var logger = new TestLogger<MediaPiAgentClient2>();
        var client = CreateClient(handler, logger);
        var device = CreateDevice(port: 0, serverKey: string.Empty);

        var response = await client.GetScheduleAsync(device, CancellationToken.None);

        Assert.That(observedUri, Is.Not.Null);
        Assert.That(observedUri!.Port, Is.EqualTo(8081));
        Assert.That(response.Ok, Is.True);
        Assert.That(logger.Entries.Any(e => e.Level == LogLevel.Warning && e.Message.Contains("invalid port")), Is.True);
        Assert.That(logger.Entries.Any(e => e.Level == LogLevel.Warning && e.Message.Contains("does not have a server key")), Is.True);
    }

    private static MediaPiAgentClient2 CreateClient(HttpMessageHandler handler, TestLogger<MediaPiAgentClient2>? logger = null)
    {
        var httpClient = new HttpClient(handler);
        return new MediaPiAgentClient2(httpClient, logger ?? new TestLogger<MediaPiAgentClient2>());
    }

    private static Device CreateDevice(string ip = "127.0.0.1", ushort port = 8085, string serverKey = "token")
    {
        return new Device
        {
            Id = 1,
            Name = "Device",
            IpAddress = ip,
            Port = port,
            ServerKey = serverKey
        };
    }

    private sealed record AudioSettings
    {
        public string? Channel { get; init; }
        public int Volume { get; init; }
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handler;

        public StubHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
        {
            _handler = handler ?? throw new ArgumentNullException(nameof(handler));
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return _handler(request, cancellationToken);
        }
    }

    private sealed class TestLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = new();

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Entries.Add((logLevel, formatter(state, exception)));
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();
            public void Dispose()
            {
            }
        }
    }
}

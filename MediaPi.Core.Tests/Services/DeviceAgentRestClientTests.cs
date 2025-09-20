// Copyright (c) 2025 sw.consulting
// This file is a part of Media Pi backend

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MediaPi.Core.Models;
using MediaPi.Core.Services;
using Microsoft.Extensions.Logging;
using NUnit.Framework;

namespace MediaPi.Core.Tests.Services;

[TestFixture]
public class DeviceAgentRestClientTests
{
    [Test]
    public void Constructor_WhenHttpClientIsNull_Throws()
    {
        var logger = new TestLogger<DeviceAgentRestClient>();

        Assert.Throws<ArgumentNullException>(() => _ = new DeviceAgentRestClient(null!, logger));
    }

    [Test]
    public void Constructor_WhenLoggerIsNull_Throws()
    {
        var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
        var httpClient = new HttpClient(handler);

        Assert.Throws<ArgumentNullException>(() => _ = new DeviceAgentRestClient(httpClient, null!));
    }

    [Test]
    public async Task ListUnitsAsync_ReturnsUnitsAndSetsAuthorizationHeader()
    {
        System.Net.Http.Headers.AuthenticationHeaderValue? observedAuth = null;
        Uri? observedUri = null;

        var handler = new StubHttpMessageHandler(async (request, cancellationToken) =>
        {
            observedAuth = request.Headers.Authorization;
            observedUri = request.RequestUri;

            const string json = """
            {
                "ok": true,
                "data": [
                    {"unit":"alpha","active":true,"sub":"child"},
                    {"unit":"beta","active":{"state":1},"sub":false,"error":"oops"}
                ]
            }
            """;

            await Task.Delay(10, cancellationToken); // Simulate some async work

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
        });

        var logger = new TestLogger<DeviceAgentRestClient>();
        var client = CreateClient(handler, logger);
        var device = CreateDevice(ip: "10.1.1.2", port: 8085, serverKey: " secret ");

        var result = await client.ListUnitsAsync(device, CancellationToken.None);

        Assert.That(result.Ok, Is.True);
        Assert.That(result.Error, Is.Null);
        Assert.That(result.Units, Has.Length.EqualTo(2));
        Assert.That(result.Units[0].Unit, Is.EqualTo("alpha"));
        Assert.That(result.Units[1].Unit, Is.EqualTo("beta"));
        Assert.That(result.Units[1].Error, Is.EqualTo("oops"));
        Assert.That(observedAuth, Is.Not.Null);
        Assert.That(observedAuth!.Scheme, Is.EqualTo("Bearer"));
        Assert.That(observedAuth.Parameter, Is.EqualTo("secret"));
        Assert.That(observedUri, Is.Not.Null);
        Assert.That(observedUri!.AbsolutePath, Is.EqualTo("/api/units"));
        Assert.That(observedUri.Port, Is.EqualTo(8085));
    }

    [Test]
    public async Task ListUnitsAsync_WhenDataMissing_ReturnsEmptyCollection()
    {
        var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"ok":true}""", Encoding.UTF8, "application/json")
        }));

        var client = CreateClient(handler);
        var device = CreateDevice();

        var result = await client.ListUnitsAsync(device, CancellationToken.None);

        Assert.That(result.Ok, Is.True);
        Assert.That(result.Units, Is.Empty);
        Assert.That(result.Error, Is.Null);
    }

    [Test]
    public async Task GetStatusAsync_WhenServerKeyMissingAndPortInvalid_LogsWarningAndUsesDefaults()
    {
        Uri? observedUri = null;
        var handler = new StubHttpMessageHandler((request, _) =>
        {
            observedUri = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"ok":false}""", Encoding.UTF8, "application/json")
            });
        });

        var logger = new TestLogger<DeviceAgentRestClient>();
        var client = CreateClient(handler, logger);
        var device = CreateDevice(ip: "192.168.0.10", port: 9999, serverKey: string.Empty);

        var result = await client.GetStatusAsync(device, " status ", CancellationToken.None);

        Assert.That(observedUri, Is.Not.Null);
        Assert.That(observedUri!.Port, Is.EqualTo(9999));
        Assert.That(observedUri.Query, Is.EqualTo("?unit=status"));
        Assert.That(result.Ok, Is.False);
        Assert.That(result.Unit, Is.EqualTo(" status "));
        Assert.That(result.Error, Is.EqualTo("Device API returned an unsuccessful response without an error message."));
        Assert.That(logger.Entries.Any(e => e.Level == LogLevel.Warning && e.Message.Contains("does not have a server key configured.")), Is.True);
        Assert.That(logger.Entries.Any(e => e.Level == LogLevel.Warning && e.Message.Contains("invalid port")), Is.True);
    }

    [Test]
    public void GetStatusAsync_WhenUnitMissing_Throws()
    {
        var client = CreateClient(new StubHttpMessageHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK))));
        var device = CreateDevice();

        Assert.ThrowsAsync<ArgumentException>(() => client.GetStatusAsync(device, "   ", CancellationToken.None));
    }

    [Test]
    public async Task CheckHealthAsync_ReturnsHealthResponse()
    {
        Uri? observedUri = null;
        var handler = new StubHttpMessageHandler((request, _) =>
        {
            observedUri = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"ok":true,"data":{"status":"healthy","uptime":12345.67,"version":"1.0.0"}}""", Encoding.UTF8, "application/json")
            });
        });

        var client = CreateClient(handler);
        var device = CreateDevice();

        var result = await client.CheckHealthAsync(device, CancellationToken.None);

        Assert.That(result.Ok, Is.True);
        Assert.That(result.Error, Is.Null);
        Assert.That(result.Status, Is.EqualTo("healthy"));
        Assert.That(result.Uptime, Is.EqualTo(12345.67));
        Assert.That(result.Version, Is.EqualTo("1.0.0"));
        Assert.That(observedUri, Is.Not.Null);
        Assert.That(observedUri!.AbsolutePath, Is.EqualTo("/health"));
    }

    [Test]
    public async Task CheckHealthAsync_WhenApiReturnsError_ReturnsError()
    {
        var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"ok":false,"error":"service unavailable"}""", Encoding.UTF8, "application/json")
        }));

        var client = CreateClient(handler);
        var device = CreateDevice();

        var result = await client.CheckHealthAsync(device, CancellationToken.None);

        Assert.That(result.Ok, Is.False);
        Assert.That(result.Error, Is.EqualTo("service unavailable"));
        Assert.That(result.Status, Is.Null);
    }

    [Test]
    public async Task CheckHealthAsync_WhenResponseEmpty_ReturnsUnsuccessful()
    {
        var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"ok":false}""", Encoding.UTF8, "application/json")
        }));

        var client = CreateClient(handler);
        var device = CreateDevice();

        var result = await client.CheckHealthAsync(device, CancellationToken.None);

        Assert.That(result.Ok, Is.False);
        Assert.That(result.Error, Is.EqualTo("Device API returned an unsuccessful response without an error message."));
    }

    [Test]
    public void CheckHealthAsync_WhenDeviceIsNull_Throws()
    {
        var client = CreateClient(new StubHttpMessageHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK))));

        Assert.ThrowsAsync<ArgumentNullException>(() => client.CheckHealthAsync(null!, CancellationToken.None));
    }

    [Test]
    public async Task StartUnitAsync_TrimsUnitNameAndReturnsResponse()
    {
        string? observedContent = null;
        HttpMethod? observedMethod = null;

        var handler = new StubHttpMessageHandler(async (request, cancellationToken) =>
        {
            observedMethod = request.Method;
            observedContent = await request.Content!.ReadAsStringAsync(cancellationToken);

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"ok":true,"data":{"unit":"alpha","result":"started"}}""", Encoding.UTF8, "application/json")
            };
        });

        var client = CreateClient(handler);
        var device = CreateDevice();

        var response = await client.StartUnitAsync(device, " alpha ", CancellationToken.None);

        Assert.That(observedMethod, Is.EqualTo(HttpMethod.Post));
        Assert.That(observedContent, Is.Not.Null);
        using var payload = JsonDocument.Parse(observedContent!);
        Assert.That(payload.RootElement.GetProperty("Unit").GetString(), Is.EqualTo("alpha"));
        Assert.That(response.Ok, Is.True);
        Assert.That(response.Unit, Is.EqualTo("alpha"));
        Assert.That(response.Result, Is.EqualTo("started"));
        Assert.That(response.Error, Is.Null);
    }

    [Test]
    public async Task StopUnitAsync_ReturnsApiResult()
    {
        var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"ok":true,"data":{"unit":"alpha","result":"stopped"}}""", Encoding.UTF8, "application/json")
        }));

        var client = CreateClient(handler);
        var device = CreateDevice();

        var response = await client.StopUnitAsync(device, "alpha", CancellationToken.None);

        Assert.That(response.Ok, Is.True);
        Assert.That(response.Unit, Is.EqualTo("alpha"));
        Assert.That(response.Result, Is.EqualTo("stopped"));
    }

    [Test]
    public async Task RestartUnitAsync_WhenApiReturnsError_UsesErrorMessage()
    {
        var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"ok":false,"error":"something wrong","data":{"unit":"alpha"}}""", Encoding.UTF8, "application/json")
        }));

        var client = CreateClient(handler);
        var device = CreateDevice();

        var response = await client.RestartUnitAsync(device, "alpha", CancellationToken.None);

        Assert.That(response.Ok, Is.False);
        Assert.That(response.Unit, Is.EqualTo("alpha"));
        Assert.That(response.Error, Is.EqualTo("something wrong"));
    }

    [Test]
    public void StartUnitAsync_WhenUnitMissing_Throws()
    {
        var client = CreateClient(new StubHttpMessageHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK))));
        var device = CreateDevice();

        Assert.ThrowsAsync<ArgumentException>(() => client.StartUnitAsync(device, "", CancellationToken.None));
    }

    [Test]
    public async Task EnableUnitAsync_WhenResultIndicatesEnabled_ReturnsEnabled()
    {
        var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"ok":true,"data":{"unit":"alpha","result":"enabled"}}""", Encoding.UTF8, "application/json")
        }));

        var client = CreateClient(handler);
        var device = CreateDevice();

        var response = await client.EnableUnitAsync(device, "alpha", CancellationToken.None);

        Assert.That(response.Ok, Is.True);
        Assert.That(response.Unit, Is.EqualTo("alpha"));
        Assert.That(response.Enabled, Is.True);
        Assert.That(response.Result, Is.EqualTo("enabled"));
        Assert.That(response.Error, Is.Null);
    }

    [Test]
    public async Task DisableUnitAsync_WhenApiFails_UsesStatusCodeMessage()
    {
        var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent("""{"ok":false,"data":{"unit":"alpha","result":"enabled"}}""", Encoding.UTF8, "application/json")
        }));

        var client = CreateClient(handler);
        var device = CreateDevice();

        var response = await client.DisableUnitAsync(device, "alpha", CancellationToken.None);

        Assert.That(response.Ok, Is.False);
        Assert.That(response.Enabled, Is.False);
        Assert.That(response.Error, Is.EqualTo("Device API responded with status 400 (BadRequest)."));
    }

    [Test]
    public void EnableUnitAsync_WhenUnitMissing_Throws()
    {
        var client = CreateClient(new StubHttpMessageHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK))));
        var device = CreateDevice();

        Assert.ThrowsAsync<ArgumentException>(() => client.EnableUnitAsync(device, "  ", CancellationToken.None));
    }

    [Test]
    public void DisableUnitAsync_WhenUnitMissing_Throws()
    {
        var client = CreateClient(new StubHttpMessageHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK))));
        var device = CreateDevice();

        Assert.ThrowsAsync<ArgumentException>(() => client.DisableUnitAsync(device, null!, CancellationToken.None));
    }

    [Test]
    public void ListUnitsAsync_WhenDeviceIsNull_Throws()
    {
        var client = CreateClient(new StubHttpMessageHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK))));

        Assert.ThrowsAsync<ArgumentNullException>(() => client.ListUnitsAsync(null!));
    }

    [Test]
    public void ListUnitsAsync_WhenIpIsMissing_Throws()
    {
        var client = CreateClient(new StubHttpMessageHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK))));
        var device = CreateDevice(ip: "   ");

        Assert.ThrowsAsync<ArgumentException>(() => client.ListUnitsAsync(device));
    }

    [Test]
    public void ListUnitsAsync_WhenIpIsInvalid_Throws()
    {
        var client = CreateClient(new StubHttpMessageHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK))));
        var device = CreateDevice(ip: "invalid");

        Assert.ThrowsAsync<InvalidOperationException>(() => client.ListUnitsAsync(device));
    }

    [Test]
    public void ListUnitsAsync_WhenResponseIsInvalidJson_Throws()
    {
        var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("not-json", Encoding.UTF8, "application/json")
        }));

        var client = CreateClient(handler);
        var device = CreateDevice();

        Assert.ThrowsAsync<InvalidOperationException>(() => client.ListUnitsAsync(device));
    }

    [Test]
    public void GetStatusAsync_WhenRequestTimesOut_Throws()
    {
        var handler = new StubHttpMessageHandler((_, _) => throw new TaskCanceledException());
        var client = CreateClient(handler);
        var device = CreateDevice();

        Assert.ThrowsAsync<TimeoutException>(() => client.GetStatusAsync(device, "unit", CancellationToken.None));
    }

    private static DeviceAgentRestClient CreateClient(HttpMessageHandler handler, TestLogger<DeviceAgentRestClient>? logger = null)
    {
        var httpClient = new HttpClient(handler);
        return new DeviceAgentRestClient(httpClient, logger ?? new TestLogger<DeviceAgentRestClient>());
    }

    private static Device CreateDevice(string? ip = "127.0.0.1", ushort port = 8080, string? serverKey = "token") => new Device
    {
        Id = 42,
        Name = "Device",
        IpAddress = ip!,
        Port = port,
        ServerKey = serverKey ?? string.Empty
    };

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handler;

        public StubHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => _handler(request, cancellationToken);
    }

    private sealed class TestLogger<T> : ILogger<T>
    {
        public sealed record LogEntry(LogLevel Level, EventId EventId, string Message, Exception? Exception);

        public IList<LogEntry> Entries { get; } = new List<LogEntry>();

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Entries.Add(new LogEntry(logLevel, eventId, formatter(state, exception), exception));
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

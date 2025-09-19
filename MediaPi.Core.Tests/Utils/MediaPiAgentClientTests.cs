// Copyright (c) 2025 sw.consulting
// This file is a part of Media Pi backend

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MediaPi.Core.Models;
using MediaPi.Core.Utils;
using NUnit.Framework;

namespace MediaPi.Core.Tests.Utils;

public class MediaPiAgentClientTests
{
    [Test]
    public void Constructor_WithNullSessionFactory_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => _ = new MediaPiAgentClient(null!));
    }

    [Test]
    public async Task ListUnitsAsync_ReturnsUnits()
    {
        var responses = new Dictionary<string, string>
        {
            ["media-pi-agent list"] =
                "{\"ok\":true,\"units\":[{\"unit\":\"mpd.service\",\"active\":\"active\",\"sub\":\"running\"},{\"unit\":\"kiosk.service\",\"error\":\"dbus unavailable\"}]}\n",
        };

        var factory = new FakeSessionFactory(command => responses.TryGetValue(command, out var value)
            ? value
            : throw new InvalidOperationException($"Unexpected command: {command}"));
        var client = new MediaPiAgentClient(factory);

        var result = await client.ListUnitsAsync(CreateDevice());

        Assert.That(result.Ok, Is.True);
        Assert.That(result.Units, Has.Count.EqualTo(2));

        var first = result.Units[0];
        Assert.Multiple(() =>
        {
            Assert.That(first.Unit, Is.EqualTo("mpd.service"));
            Assert.That(first.Active.ValueKind, Is.EqualTo(JsonValueKind.String));
            Assert.That(first.ActiveState, Is.EqualTo("active"));
            Assert.That(first.Sub.ValueKind, Is.EqualTo(JsonValueKind.String));
            Assert.That(first.SubState, Is.EqualTo("running"));
            Assert.That(first.Error, Is.Null);
        });

        var second = result.Units[1];
        Assert.Multiple(() =>
        {
            Assert.That(second.Unit, Is.EqualTo("kiosk.service"));
            Assert.That(second.Active.ValueKind, Is.EqualTo(JsonValueKind.Undefined));
            Assert.That(second.Sub.ValueKind, Is.EqualTo(JsonValueKind.Undefined));
            Assert.That(second.ActiveState, Is.Null);
            Assert.That(second.SubState, Is.Null);
            Assert.That(second.Error, Is.EqualTo("dbus unavailable"));
        });

        Assert.That(factory.Commands, Is.EqualTo(new[] { "media-pi-agent list" }));
    }

    [Test]
    public async Task ListUnitsAsync_ReturnsStructuredStatesWhenProvided()
    {
        var responses = new Dictionary<string, string>
        {
            ["media-pi-agent list"] =
                "{\"ok\":true,\"units\":[{\"unit\":\"mpd.service\",\"active\":{\"Sig\":\"s\",\"Value\":\"active\"},\"sub\":{\"Sig\":\"s\",\"Value\":\"running\"}}]}\n",
        };

        var factory = new FakeSessionFactory(command => responses.TryGetValue(command, out var value)
            ? value
            : throw new InvalidOperationException($"Unexpected command: {command}"));
        var client = new MediaPiAgentClient(factory);

        var result = await client.ListUnitsAsync(CreateDevice());

        Assert.That(result.Ok, Is.True);
        Assert.That(result.Units, Has.Count.EqualTo(1));

        var unit = result.Units[0];
        Assert.That(unit.Active.ValueKind, Is.EqualTo(JsonValueKind.Object));
        Assert.That(unit.ActiveState, Is.Null);
        Assert.That(unit.Active.TryGetProperty("Value", out var activeValue), Is.True);
        Assert.That(activeValue.GetString(), Is.EqualTo("active"));

        Assert.That(unit.Sub.ValueKind, Is.EqualTo(JsonValueKind.Object));
        Assert.That(unit.SubState, Is.Null);
        Assert.That(unit.Sub.TryGetProperty("Value", out var subValue), Is.True);
        Assert.That(subValue.GetString(), Is.EqualTo("running"));
    }

    [Test]
    public void ListUnitsAsync_WithNullDevice_Throws()
    {
        var factory = new FakeSessionFactory(_ => throw new InvalidOperationException("Should not execute"));
        var client = new MediaPiAgentClient(factory);

        Assert.ThrowsAsync<ArgumentNullException>(() => client.ListUnitsAsync(null!));
    }

    [Test]
    public void ListUnitsAsync_WithWhitespaceResponse_ThrowsInvalidOperationException()
    {
        var factory = new FakeSessionFactory(_ => "   \n\t");
        var client = new MediaPiAgentClient(factory);

        var exception = Assert.ThrowsAsync<InvalidOperationException>(() => client.ListUnitsAsync(CreateDevice()));

        Assert.That(exception!.Message, Does.Contain("returned no output"));
    }

    [Test]
    public void ListUnitsAsync_WithInvalidJson_ThrowsInvalidOperationException()
    {
        var factory = new FakeSessionFactory(_ => "{\"ok\": tru}");
        var client = new MediaPiAgentClient(factory);

        var exception = Assert.ThrowsAsync<InvalidOperationException>(() => client.ListUnitsAsync(CreateDevice()));

        Assert.That(exception!.Message, Does.Contain("Failed to parse JSON response"));
        Assert.That(exception.InnerException, Is.InstanceOf<JsonException>());
    }

    [Test]
    public async Task GetStatusAsync_ReturnsStatusDetails()
    {
        var responses = new Dictionary<string, string>
        {
            ["media-pi-agent status \"mpd.service\""] = "{\"ok\":true,\"unit\":\"mpd.service\",\"active\":\"active\",\"sub\":\"running\"}",
        };

        var factory = new FakeSessionFactory(command => responses.TryGetValue(command, out var value)
            ? value
            : throw new InvalidOperationException($"Unexpected command: {command}"));
        var client = new MediaPiAgentClient(factory);

        var result = await client.GetStatusAsync(CreateDevice(), "mpd.service");

        Assert.That(result.Ok, Is.True);
        Assert.That(result.Active.ValueKind, Is.EqualTo(JsonValueKind.String));
        Assert.That(result.ActiveState, Is.EqualTo("active"));
        Assert.That(result.Sub.ValueKind, Is.EqualTo(JsonValueKind.String));
        Assert.That(result.SubState, Is.EqualTo("running"));
        Assert.That(factory.Commands, Is.EqualTo(new[] { "media-pi-agent status \"mpd.service\"" }));
    }

    [Test]
    public async Task StartUnitAsync_QuotesUnitName()
    {
        var responses = new Dictionary<string, string>
        {
            ["media-pi-agent start \"mpd@foo.service\""] = "{\"ok\":true,\"unit\":\"mpd@foo.service\",\"result\":\"done\"}",
        };

        var factory = new FakeSessionFactory(command => responses.TryGetValue(command, out var value)
            ? value
            : throw new InvalidOperationException($"Unexpected command: {command}"));
        var client = new MediaPiAgentClient(factory);

        var response = await client.StartUnitAsync(CreateDevice(), "mpd@foo.service");

        Assert.That(response.Ok, Is.True);
        Assert.That(response.Unit, Is.EqualTo("mpd@foo.service"));
        Assert.That(response.Result, Is.EqualTo("done"));
        Assert.That(factory.Commands, Is.EqualTo(new[] { "media-pi-agent start \"mpd@foo.service\"" }));
    }

    [Test]
    public async Task RestartUnitAsync_EscapesQuotesInUnitName()
    {
        const string expectedCommand = "media-pi-agent restart \"mpd\\\"special.service\"";
        const string responseText = "{\"ok\":true,\"unit\":\"mpd\\\"special.service\",\"result\":\"restarted\"}";

        var factory = new FakeSessionFactory(command =>
        {
            Assert.That(command, Is.EqualTo(expectedCommand));
            return responseText;
        });
        var client = new MediaPiAgentClient(factory);

        var response = await client.RestartUnitAsync(CreateDevice(), "mpd\"special.service");

        Assert.That(response.Result, Is.EqualTo("restarted"));
        Assert.That(factory.Commands, Is.EqualTo(new[] { "media-pi-agent restart \"mpd\\\"special.service\"" }));
    }

    [Test]
    public async Task GetStatusAsync_ReturnsErrorInformation()
    {
        var responses = new Dictionary<string, string>
        {
            ["media-pi-agent status \"forbidden.service\""] = "{\"ok\":false,\"error\":\"unit forbidden\"}",
        };

        var factory = new FakeSessionFactory(command => responses.TryGetValue(command, out var value)
            ? value
            : throw new InvalidOperationException($"Unexpected command: {command}"));
        var client = new MediaPiAgentClient(factory);

        var response = await client.GetStatusAsync(CreateDevice(), "forbidden.service");

        Assert.That(response.Ok, Is.False);
        Assert.That(response.Error, Is.EqualTo("unit forbidden"));
    }

    [Test]
    public async Task EnableUnitAsync_ReturnsEnabledState()
    {
        var responses = new Dictionary<string, string>
        {
            ["media-pi-agent enable \"mpd.service\""] = "{\"ok\":true,\"unit\":\"mpd.service\",\"enabled\":true}",
        };

        var factory = new FakeSessionFactory(command => responses.TryGetValue(command, out var value)
            ? value
            : throw new InvalidOperationException($"Unexpected command: {command}"));
        var client = new MediaPiAgentClient(factory);

        var response = await client.EnableUnitAsync(CreateDevice(), "mpd.service");

        Assert.That(response.Ok, Is.True);
        Assert.That(response.Enabled, Is.True);
    }

    [Test]
    public void GetStatusAsync_WithEmptyUnit_Throws()
    {
        var factory = new FakeSessionFactory(_ => throw new InvalidOperationException("Should not execute"));
        var client = new MediaPiAgentClient(factory);

        Assert.ThrowsAsync<ArgumentException>(() => client.GetStatusAsync(CreateDevice(), ""));
    }

    [Test]
    public async Task GetStatusAsync_TrimsUnitBeforeSending()
    {
        var responses = new Dictionary<string, string>
        {
            ["media-pi-agent status \"mpd.service\""] = "{\"ok\":true,\"unit\":\"mpd.service\",\"active\":\"active\",\"sub\":\"running\"}",
        };

        var factory = new FakeSessionFactory(command => responses.TryGetValue(command, out var value)
            ? value
            : throw new InvalidOperationException($"Unexpected command: {command}"));
        var client = new MediaPiAgentClient(factory);

        await client.GetStatusAsync(CreateDevice(), "  mpd.service  ");

        Assert.That(factory.Commands, Is.EqualTo(new[] { "media-pi-agent status \"mpd.service\"" }));
    }

    private static Device CreateDevice() => new()
    {
        Id = 1,
        Name = "Device 1",
        IpAddress = "127.0.0.1",
        PublicKeyOpenSsh = "ssh-rsa AAAAB3NzaC1yc2EAAAADAQABAAABAQDdummy",
        SshUser = "pi",
    };

    private sealed class FakeSessionFactory : ISshSessionFactory
    {
        private readonly Func<string, string> _responseFactory;

        public FakeSessionFactory(Func<string, string> responseFactory)
        {
            _responseFactory = responseFactory;
        }

        public IList<string> Commands { get; } = new List<string>();

        public Task<ISshSession> CreateAsync(Device device, CancellationToken cancellationToken) =>
            Task.FromResult<ISshSession>(new FakeSession(_responseFactory, Commands));

        private sealed class FakeSession : ISshSession
        {
            private readonly Func<string, string> _responseFactory;
            private readonly IList<string> _commands;

            public FakeSession(Func<string, string> responseFactory, IList<string> commands)
            {
                _responseFactory = responseFactory;
                _commands = commands;
            }

            public Task<string> ExecuteAsync(string command, CancellationToken cancellationToken)
            {
                _commands.Add(command);
                return Task.FromResult(_responseFactory(command));
            }

            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }
}

public class SshNetSessionFactoryTests
{
    [Test]
    public void Deprecated_Test_Removed()
    {
        Assert.Pass("SshNetSessionFactory now requires DI provided dependencies; direct construction test removed.");
    }
}

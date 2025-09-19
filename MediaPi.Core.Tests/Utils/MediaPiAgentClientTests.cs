using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediaPi.Core.Models;
using MediaPi.Core.Utils;
using NUnit.Framework;

namespace MediaPi.Core.Tests.Utils;

public class MediaPiAgentClientTests
{
    [Test]
    public async Task ListUnitsAsync_ReturnsUnits()
    {
        var responses = new Dictionary<string, string>
        {
            ["media-pi-agent list"] = "{\"ok\":true,\"units\":[\"mpd.service\",\"kiosk.service\"]}\n",
        };

        var factory = new FakeSessionFactory(command => responses.TryGetValue(command, out var value)
            ? value
            : throw new InvalidOperationException($"Unexpected command: {command}"));
        var client = new MediaPiAgentClient(factory);

        var result = await client.ListUnitsAsync(CreateDevice());

        Assert.That(result.Ok, Is.True);
        Assert.That(result.Units, Is.EqualTo(new[] { "mpd.service", "kiosk.service" }));
        Assert.That(factory.Commands, Is.EqualTo(new[] { "media-pi-agent list" }));
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
        Assert.That(result.Active, Is.EqualTo("active"));
        Assert.That(result.Sub, Is.EqualTo("running"));
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
    public void CreateAsync_MissingPublicKey_Throws()
    {
        var device = new Device
        {
            Id = 1,
            Name = "Device",
            IpAddress = "127.0.0.1",
            PublicKeyOpenSsh = string.Empty,
            SshUser = "pi",
        };

        var factory = new SshNetSessionFactory();

        Assert.ThrowsAsync<InvalidOperationException>(() => factory.CreateAsync(device, CancellationToken.None));
    }
}

// Copyright (c) 2025 sw.consulting
// This file is a part of Media Pi backend

using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using MediaPi.Core.Models;
using MediaPi.Core.Services; 
using Microsoft.Extensions.Logging;
using Renci.SshNet;
using Renci.SshNet.Async;
using SshConnectionInfo = Renci.SshNet.ConnectionInfo;

namespace MediaPi.Core.Utils;

public interface IMediaPiAgentClient
{
    Task<MediaPiAgentListResponse> ListUnitsAsync(Device device, CancellationToken cancellationToken = default);
    Task<MediaPiAgentStatusResponse> GetStatusAsync(Device device, string unit, CancellationToken cancellationToken = default);
    Task<MediaPiAgentUnitResultResponse> StartUnitAsync(Device device, string unit, CancellationToken cancellationToken = default);
    Task<MediaPiAgentUnitResultResponse> StopUnitAsync(Device device, string unit, CancellationToken cancellationToken = default);
    Task<MediaPiAgentUnitResultResponse> RestartUnitAsync(Device device, string unit, CancellationToken cancellationToken = default);
    Task<MediaPiAgentEnableResponse> EnableUnitAsync(Device device, string unit, CancellationToken cancellationToken = default);
    Task<MediaPiAgentEnableResponse> DisableUnitAsync(Device device, string unit, CancellationToken cancellationToken = default);
}

public sealed class MediaPiAgentClient : IMediaPiAgentClient
{
    private const string AgentExecutable = "media-pi-agent";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly ISshSessionFactory _sessionFactory;

    public MediaPiAgentClient(ISshSessionFactory sessionFactory)
    {
        _sessionFactory = sessionFactory ?? throw new ArgumentNullException(nameof(sessionFactory));
    }

    public Task<MediaPiAgentListResponse> ListUnitsAsync(Device device, CancellationToken cancellationToken = default) =>
        ExecuteAsync<MediaPiAgentListResponse>(device, cancellationToken, "list");

    public Task<MediaPiAgentStatusResponse> GetStatusAsync(Device device, string unit, CancellationToken cancellationToken = default) =>
        ExecuteAsync<MediaPiAgentStatusResponse>(device, cancellationToken, "status", EnsureUnit(unit));

    public Task<MediaPiAgentUnitResultResponse> StartUnitAsync(Device device, string unit, CancellationToken cancellationToken = default) =>
        ExecuteAsync<MediaPiAgentUnitResultResponse>(device, cancellationToken, "start", EnsureUnit(unit));

    public Task<MediaPiAgentUnitResultResponse> StopUnitAsync(Device device, string unit, CancellationToken cancellationToken = default) =>
        ExecuteAsync<MediaPiAgentUnitResultResponse>(device, cancellationToken, "stop", EnsureUnit(unit));

    public Task<MediaPiAgentUnitResultResponse> RestartUnitAsync(Device device, string unit, CancellationToken cancellationToken = default) =>
        ExecuteAsync<MediaPiAgentUnitResultResponse>(device, cancellationToken, "restart", EnsureUnit(unit));

    public Task<MediaPiAgentEnableResponse> EnableUnitAsync(Device device, string unit, CancellationToken cancellationToken = default) =>
        ExecuteAsync<MediaPiAgentEnableResponse>(device, cancellationToken, "enable", EnsureUnit(unit));

    public Task<MediaPiAgentEnableResponse> DisableUnitAsync(Device device, string unit, CancellationToken cancellationToken = default) =>
        ExecuteAsync<MediaPiAgentEnableResponse>(device, cancellationToken, "disable", EnsureUnit(unit));

    private async Task<TResponse> ExecuteAsync<TResponse>(Device device, CancellationToken cancellationToken, string command, params string[] args)
    {
        if (device is null) throw new ArgumentNullException(nameof(device));
        if (string.IsNullOrWhiteSpace(command)) throw new ArgumentException("Command must be provided.", nameof(command));

        var commandText = BuildCommand(command, args);

        await using var session = await _sessionFactory.CreateAsync(device, cancellationToken).ConfigureAwait(false);
        var rawOutput = await session.ExecuteAsync(commandText, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(rawOutput))
        {
            throw new InvalidOperationException($"SSH command '{commandText}' returned no output.");
        }

        var trimmed = rawOutput.Trim();
        try
        {
            return JsonSerializer.Deserialize<TResponse>(trimmed, JsonOptions)
                   ?? throw new InvalidOperationException($"Unable to deserialize response for command '{command}'.");
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"Failed to parse JSON response from '{command}' command.", ex);
        }
    }

    private static string EnsureUnit(string unit)
    {
        if (string.IsNullOrWhiteSpace(unit))
        {
            throw new ArgumentException("Unit name must be provided.", nameof(unit));
        }
        return unit.Trim();
    }

    private static string BuildCommand(string command, params string[] args)
    {
        var builder = new StringBuilder();
        builder.Append(AgentExecutable);
        builder.Append(' ');
        builder.Append(command);

        if (args is { Length: > 0 })
        {
            foreach (var arg in args)
            {
                builder.Append(' ');
                builder.Append(EscapeArgument(arg));
            }
        }
        return builder.ToString();
    }

    private static string EscapeArgument(string value)
    {
        var escaped = value.Replace("\"", "\\\"");
        return $"\"{escaped}\"";
    }
}

public interface ISshSessionFactory
{
    Task<ISshSession> CreateAsync(Device device, CancellationToken cancellationToken);
}

public interface ISshSession : IAsyncDisposable
{
    Task<string> ExecuteAsync(string command, CancellationToken cancellationToken);
}

public sealed class SshNetSessionFactory : ISshSessionFactory
{
    private readonly ISshClientKeyProvider _keyProvider;
    private readonly ILogger<SshNetSessionFactory> _logger;

    public SshNetSessionFactory(ISshClientKeyProvider keyProvider, ILogger<SshNetSessionFactory> logger)
    {
        _keyProvider = keyProvider ?? throw new ArgumentNullException(nameof(keyProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ISshSession> CreateAsync(Device device, CancellationToken cancellationToken)
    {
        if (device is null) throw new ArgumentNullException(nameof(device));
        if (string.IsNullOrWhiteSpace(device.IpAddress))
            throw new ArgumentException("Device IP address must be provided.", nameof(device));

        var user = string.IsNullOrWhiteSpace(device.SshUser) ? "pi" : device.SshUser.Trim();
        if (string.IsNullOrWhiteSpace(user))
            throw new InvalidOperationException("SSH user name is missing.");

        var privateKeyPath = _keyProvider.GetPrivateKeyPath();
        if (string.IsNullOrWhiteSpace(privateKeyPath) || !File.Exists(privateKeyPath))
            throw new InvalidOperationException("SSH client private key not found or not configured.");

        PrivateKeyFile privateKeyFile;
        try
        {
            privateKeyFile = new PrivateKeyFile(privateKeyPath);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to load SSH client private key.", ex);
        }

        var authentication = new PrivateKeyAuthenticationMethod(user, privateKeyFile);
        var connectionInfo = new SshConnectionInfo(device.IpAddress, user, authentication);

        var client = new SshClient(connectionInfo);

        // Optional host key pinning using device.PublicKeyOpenSsh (treat it as host public key line if present)
        if (!string.IsNullOrWhiteSpace(device.PublicKeyOpenSsh))
        {
            var parts = device.PublicKeyOpenSsh.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
            {
                var expectedAlgo = parts[0];
                var expectedBody = parts[1];
                client.HostKeyReceived += (s, e) =>
                {
                    try
                    {
                        if (!string.Equals(expectedAlgo, e.HostKeyName, StringComparison.Ordinal))
                        {
                            _logger.LogWarning("Host key algo mismatch for {Ip}: expected {Expected}, got {Actual}", device.IpAddress, expectedAlgo, e.HostKeyName);
                            e.CanTrust = false;
                            return;
                        }
                        var actualBody = Convert.ToBase64String(e.HostKey);
                        if (!string.Equals(actualBody, expectedBody, StringComparison.Ordinal))
                        {
                            _logger.LogWarning("Host key body mismatch for {Ip}", device.IpAddress);
                            e.CanTrust = false;
                            return;
                        }
                        e.CanTrust = true;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error validating host key for {Ip}", device.IpAddress);
                        e.CanTrust = false;
                    }
                };
            }
        }

        try
        {
            await Task.Run(() => client.Connect(), cancellationToken).ConfigureAwait(false);
            return new SshNetSession(client, privateKeyFile);
        }
        catch
        {
            client.Dispose();
            privateKeyFile.Dispose();
            throw;
        }
    }
}

internal sealed class SshNetSession : ISshSession
{
    private readonly SshClient _client;
    private readonly PrivateKeyFile _privateKeyFile;

    public SshNetSession(SshClient client, PrivateKeyFile privateKeyFile)
    {
        _client = client;
        _privateKeyFile = privateKeyFile;
    }

    public async Task<string> ExecuteAsync(string command, CancellationToken cancellationToken)
    {
        using var sshCommand = _client.CreateCommand(command);
        var factory = new TaskFactory<string>(cancellationToken, TaskCreationOptions.None, TaskContinuationOptions.None, TaskScheduler.Default);
        var output = await sshCommand.ExecuteAsync(factory).ConfigureAwait(false);
        if (string.IsNullOrEmpty(output))
        {
            output = sshCommand.Error;
        }
        if (string.IsNullOrWhiteSpace(output))
        {
            throw new InvalidOperationException($"SSH command '{command}' returned no output.");
        }
        return output;
    }

    public ValueTask DisposeAsync()
    {
        _client.Dispose();
        _privateKeyFile.Dispose();
        return ValueTask.CompletedTask;
    }
}

public record class MediaPiAgentResponse
{
    [JsonPropertyName("ok")] public bool Ok { get; init; }
    [JsonPropertyName("error")] public string? Error { get; init; }
}

public record class MediaPiAgentListResponse : MediaPiAgentResponse
{
    [JsonPropertyName("units")] public IReadOnlyList<MediaPiAgentListUnit> Units { get; init; } = Array.Empty<MediaPiAgentListUnit>();
}

public record class MediaPiAgentListUnit
{
    [JsonPropertyName("unit")] public string? Unit { get; init; }
    [JsonPropertyName("active")] public JsonElement Active { get; init; }
    [JsonIgnore] public string? ActiveState => Active.ValueKind == JsonValueKind.String ? Active.GetString() : null;
    [JsonPropertyName("sub")] public JsonElement Sub { get; init; }
    [JsonIgnore] public string? SubState => Sub.ValueKind == JsonValueKind.String ? Sub.GetString() : null;
    [JsonPropertyName("error")] public string? Error { get; init; }
}

public record class MediaPiAgentStatusResponse : MediaPiAgentResponse
{
    [JsonPropertyName("unit")] public string? Unit { get; init; }
    [JsonPropertyName("active")] public JsonElement Active { get; init; }
    [JsonIgnore] public string? ActiveState => Active.ValueKind == JsonValueKind.String ? Active.GetString() : null;
    [JsonPropertyName("sub")] public JsonElement Sub { get; init; }
    [JsonIgnore] public string? SubState => Sub.ValueKind == JsonValueKind.String ? Sub.GetString() : null;
}

public record class MediaPiAgentUnitResultResponse : MediaPiAgentResponse
{
    [JsonPropertyName("unit")] public string? Unit { get; init; }
    [JsonPropertyName("result")] public string? Result { get; init; }
}

public record class MediaPiAgentEnableResponse : MediaPiAgentResponse
{
    [JsonPropertyName("unit")] public string? Unit { get; init; }
    [JsonPropertyName("enabled")] public bool? Enabled { get; init; }
}

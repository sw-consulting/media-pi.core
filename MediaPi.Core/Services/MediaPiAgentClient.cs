// Copyright (c) 2025 sw.consulting
// This file is a part of Media Pi backend

using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using MediaPi.Core.Models;
using MediaPi.Core.Services.Interfaces;
using MediaPi.Core.Services.Models;

namespace MediaPi.Core.Services;

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

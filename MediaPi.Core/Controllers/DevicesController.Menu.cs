// Copyright (c) 2025 sw.consulting
// This file is a part of Media Pi backend

using System.Text.Json;
using MediaPi.Core.RestModels;
using MediaPi.Core.Services.Models;
using Microsoft.AspNetCore.Mvc;

namespace MediaPi.Core.Controllers;

public partial class DevicesController
{
    [HttpPost("{id}/playback/stop")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(MediaPiMenuCommandResponse))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status502BadGateway, Type = typeof(ErrMessage))]
    public Task<ActionResult<MediaPiMenuCommandResponse>> StopPlayback(int id, CancellationToken ct = default)
    {
        return ExecuteAgentOperation(
            id,
            "stop playback",
            (device, token) => mediaPiAgentClient2.StopPlaybackAsync(device, token),
            ct);
    }

    [HttpPost("{id}/playback/start")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(MediaPiMenuCommandResponse))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status502BadGateway, Type = typeof(ErrMessage))]
    public Task<ActionResult<MediaPiMenuCommandResponse>> StartPlayback(int id, CancellationToken ct = default)
    {
        return ExecuteAgentOperation(
            id,
            "start playback",
            (device, token) => mediaPiAgentClient2.StartPlaybackAsync(device, token),
            ct);
    }

    [HttpGet("{id}/storage/check")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(MediaPiMenuDataResponse))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status502BadGateway, Type = typeof(ErrMessage))]
    public Task<ActionResult<MediaPiMenuDataResponse>> CheckStorage(int id, CancellationToken ct = default)
    {
        return ExecuteAgentOperation(
            id,
            "check storage",
            (device, token) => mediaPiAgentClient2.CheckStorageAsync(device, token),
            ct);
    }

    [HttpGet("{id}/playlist/get")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(MediaPiMenuDataResponse))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status502BadGateway, Type = typeof(ErrMessage))]
    public Task<ActionResult<MediaPiMenuDataResponse>> GetPlaylistSettings(int id, CancellationToken ct = default)
    {
        return ExecuteAgentOperation(
            id,
            "get playlist settings",
            (device, token) => mediaPiAgentClient2.GetPlaylistSettingsAsync(device, token),
            ct);
    }

    [HttpPut("{id}/playlist/update")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(MediaPiMenuCommandResponse))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status502BadGateway, Type = typeof(ErrMessage))]
    public Task<ActionResult<MediaPiMenuCommandResponse>> UpdatePlaylistSettings(int id, [FromBody] JsonElement payload, CancellationToken ct = default)
    {
        if (IsPayloadMissing(payload))
        {
            return Task.FromResult<ActionResult<MediaPiMenuCommandResponse>>(_400RequestPayloadMissing());
        }

        var payloadClone = payload.Clone();
        return ExecuteAgentOperation(
            id,
            "update playlist settings",
            (device, token) => mediaPiAgentClient2.UpdatePlaylistSettingsAsync(device, payloadClone, token),
            ct);
    }

    [HttpPost("{id}/playlist/start-upload")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(MediaPiMenuCommandResponse))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status502BadGateway, Type = typeof(ErrMessage))]
    public Task<ActionResult<MediaPiMenuCommandResponse>> StartPlaylistUpload(int id, CancellationToken ct = default)
    {
        return ExecuteAgentOperation(
            id,
            "start playlist upload",
            (device, token) => mediaPiAgentClient2.StartPlaylistUploadAsync(device, token),
            ct);
    }

    [HttpPost("{id}/playlist/stop-upload")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(MediaPiMenuCommandResponse))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status502BadGateway, Type = typeof(ErrMessage))]
    public Task<ActionResult<MediaPiMenuCommandResponse>> StopPlaylistUpload(int id, CancellationToken ct = default)
    {
        return ExecuteAgentOperation(
            id,
            "stop playlist upload",
            (device, token) => mediaPiAgentClient2.StopPlaylistUploadAsync(device, token),
            ct);
    }

    [HttpGet("{id}/schedule/get")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(MediaPiMenuDataResponse))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status502BadGateway, Type = typeof(ErrMessage))]
    public Task<ActionResult<MediaPiMenuDataResponse>> GetSchedule(int id, CancellationToken ct = default)
    {
        return ExecuteAgentOperation(
            id,
            "get schedule",
            (device, token) => mediaPiAgentClient2.GetScheduleAsync(device, token),
            ct);
    }

    [HttpPut("{id}/schedule/update")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(MediaPiMenuCommandResponse))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status502BadGateway, Type = typeof(ErrMessage))]
    public Task<ActionResult<MediaPiMenuCommandResponse>> UpdateSchedule(int id, [FromBody] JsonElement payload, CancellationToken ct = default)
    {
        if (IsPayloadMissing(payload))
        {
            return Task.FromResult<ActionResult<MediaPiMenuCommandResponse>>(_400RequestPayloadMissing());
        }

        var payloadClone = payload.Clone();
        return ExecuteAgentOperation(
            id,
            "update schedule",
            (device, token) => mediaPiAgentClient2.UpdateScheduleAsync(device, payloadClone, token),
            ct);
    }

    [HttpGet("{id}/audio/get")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(MediaPiMenuDataResponse))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status502BadGateway, Type = typeof(ErrMessage))]
    public Task<ActionResult<MediaPiMenuDataResponse>> GetAudioSettings(int id, CancellationToken ct = default)
    {
        return ExecuteAgentOperation(
            id,
            "get audio settings",
            (device, token) => mediaPiAgentClient2.GetAudioSettingsAsync(device, token),
            ct);
    }

    [HttpPut("{id}/audio/update")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(MediaPiMenuCommandResponse))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status502BadGateway, Type = typeof(ErrMessage))]
    public Task<ActionResult<MediaPiMenuCommandResponse>> UpdateAudioSettings(int id, [FromBody] JsonElement payload, CancellationToken ct = default)
    {
        if (IsPayloadMissing(payload))
        {
            return Task.FromResult<ActionResult<MediaPiMenuCommandResponse>>(_400RequestPayloadMissing());
        }

        var payloadClone = payload.Clone();
        return ExecuteAgentOperation(
            id,
            "update audio settings",
            (device, token) => mediaPiAgentClient2.UpdateAudioSettingsAsync(device, payloadClone, token),
            ct);
    }

    [HttpPost("{id}/system/reload")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(MediaPiMenuCommandResponse))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status502BadGateway, Type = typeof(ErrMessage))]
    public Task<ActionResult<MediaPiMenuCommandResponse>> ReloadSystem(int id, CancellationToken ct = default)
    {
        return ExecuteAgentOperation(
            id,
            "reload system",
            (device, token) => mediaPiAgentClient2.ReloadSystemAsync(device, token),
            ct);
    }

    [HttpPost("{id}/system/reboot")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(MediaPiMenuCommandResponse))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status502BadGateway, Type = typeof(ErrMessage))]
    public Task<ActionResult<MediaPiMenuCommandResponse>> RebootSystem(int id, CancellationToken ct = default)
    {
        return ExecuteAgentOperation(
            id,
            "reboot system",
            (device, token) => mediaPiAgentClient2.RebootSystemAsync(device, token),
            ct);
    }

    [HttpPost("{id}/system/shutdown")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(MediaPiMenuCommandResponse))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status502BadGateway, Type = typeof(ErrMessage))]
    public Task<ActionResult<MediaPiMenuCommandResponse>> ShutdownSystem(int id, CancellationToken ct = default)
    {
        return ExecuteAgentOperation(
            id,
            "shutdown system",
            (device, token) => mediaPiAgentClient2.ShutdownSystemAsync(device, token),
            ct);
    }

    private static bool IsPayloadMissing(JsonElement payload) =>
        payload.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null;
}

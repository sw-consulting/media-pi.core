// Copyright (c) 2025 sw.consulting
// This file is a part of Media Pi backend

using System.Text.Json;
using MediaPi.Core.RestModels;
using MediaPi.Core.RestModels.Device;
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

    [HttpGet("{id}/playlist/get")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(PlaylistSettingsDto))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status502BadGateway, Type = typeof(ErrMessage))]
    public Task<ActionResult<PlaylistSettingsDto>> GetPlaylistSettings(int id, CancellationToken ct = default)
    {
        return ExecuteAgentDataOperation(
            id,
            "get playlist settings",
            (device, token) => mediaPiAgentClient2.GetPlaylistSettingsAsync(device, token),
            "Устройство не вернуло данные настроек плейлиста",
            ct);
    }

    [HttpPut("{id}/playlist/update")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(MediaPiMenuCommandResponse))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status502BadGateway, Type = typeof(ErrMessage))]
    public Task<ActionResult<MediaPiMenuCommandResponse>> UpdatePlaylistSettings(int id, [FromBody] PlaylistSettingsDto payload, CancellationToken ct = default)
    {
        return ExecuteAgentOperation(
            id,
            "update playlist settings",
            (device, token) => mediaPiAgentClient2.UpdatePlaylistSettingsAsync(device, payload, token),
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
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ScheduleSettingsDto))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status502BadGateway, Type = typeof(ErrMessage))]
    public Task<ActionResult<ScheduleSettingsDto>> GetSchedule(int id, CancellationToken ct = default)
    {
        return ExecuteAgentDataOperation(
            id,
            "get schedule",
            (device, token) => mediaPiAgentClient2.GetScheduleAsync(device, token),
            "Устройство не вернуло данные расписания",
            ct);
    }

    [HttpPut("{id}/schedule/update")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(MediaPiMenuCommandResponse))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status502BadGateway, Type = typeof(ErrMessage))]
    public Task<ActionResult<MediaPiMenuCommandResponse>> UpdateSchedule(int id, [FromBody] ScheduleSettingsDto payload, CancellationToken ct = default)
    {
        return ExecuteAgentOperation(
            id,
            "update schedule",
            (device, token) => mediaPiAgentClient2.UpdateScheduleAsync(device, payload, token),
            ct);
    }

    [HttpGet("{id}/audio/get")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(AudioSettingsDto))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status502BadGateway, Type = typeof(ErrMessage))]
    public Task<ActionResult<AudioSettingsDto>> GetAudioSettings(int id, CancellationToken ct = default)
    {
        return ExecuteAgentDataOperation(
            id,
            "get audio settings",
            (device, token) => mediaPiAgentClient2.GetAudioSettingsAsync(device, token),
            "Устройство не вернуло данные настроек аудио",
            ct);
    }

    [HttpPut("{id}/audio/update")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(MediaPiMenuCommandResponse))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status502BadGateway, Type = typeof(ErrMessage))]
    public Task<ActionResult<MediaPiMenuCommandResponse>> UpdateAudioSettings(int id, [FromBody] AudioSettingsDto payload, CancellationToken ct = default)
    {
        return ExecuteAgentOperation(
            id,
            "update audio settings",
            (device, token) => mediaPiAgentClient2.UpdateAudioSettingsAsync(device, payload, token),
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

    [HttpGet("{id}/service/status")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ServiceStatusDto))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status502BadGateway, Type = typeof(ErrMessage))]
    public Task<ActionResult<ServiceStatusDto>> GetServiceStatus(int id, CancellationToken ct = default)
    {
        return ExecuteAgentDataOperation(
            id,
            "get service status",
            (device, token) => mediaPiAgentClient2.GetServiceStatusAsync(device, token),
            "Устройство не вернуло данные статуса сервисов",
            ct);
    }

    private static bool IsPayloadMissing(JsonElement payload) =>
        payload.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null;
}

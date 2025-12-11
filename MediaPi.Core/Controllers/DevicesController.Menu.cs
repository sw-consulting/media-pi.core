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

    [HttpPost("{id}/video/start-upload")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(MediaPiMenuCommandResponse))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status502BadGateway, Type = typeof(ErrMessage))]
    public Task<ActionResult<MediaPiMenuCommandResponse>> StartVideoUpload(int id, CancellationToken ct = default)
    {
        return ExecuteAgentOperation(
            id,
            "start video upload",
            (device, token) => mediaPiAgentClient2.StartVideoUploadAsync(device, token),
            ct);
    }

    [HttpPost("{id}/video/stop-upload")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(MediaPiMenuCommandResponse))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status502BadGateway, Type = typeof(ErrMessage))]
    public Task<ActionResult<MediaPiMenuCommandResponse>> StopVideoUpload(int id, CancellationToken ct = default)
    {
        return ExecuteAgentOperation(
            id,
            "stop video upload",
            (device, token) => mediaPiAgentClient2.StopVideoUploadAsync(device, token),
            ct);
    }

    [HttpGet("{id}/configuration/get")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ConfigurationSettingsDto))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status502BadGateway, Type = typeof(ErrMessage))]
    public Task<ActionResult<ConfigurationSettingsDto>> GetConfiguration(int id, CancellationToken ct = default)
    {
        return ExecuteAgentDataOperation(
            id,
            "get configuration",
            (device, token) => mediaPiAgentClient2.GetConfigurationAsync(device, token),
            "Устройство не вернуло данные конфигурации",
            ct);
    }

    [HttpPut("{id}/configuration/update")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(MediaPiMenuCommandResponse))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status502BadGateway, Type = typeof(ErrMessage))]
    public Task<ActionResult<MediaPiMenuCommandResponse>> UpdateConfiguration(int id, [FromBody] ConfigurationSettingsDto payload, CancellationToken ct = default)
    {
        return ExecuteAgentOperation(
            id,
            "update configuration",
            (device, token) => mediaPiAgentClient2.UpdateConfigurationAsync(device, payload, token),
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

}

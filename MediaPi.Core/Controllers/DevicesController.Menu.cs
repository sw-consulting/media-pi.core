// Copyright (C) 2025-2026 sw.consulting
// This file is a part of Media Pi backend

using System.Text.Json;
using MediaPi.Core.Models;
using MediaPi.Core.RestModels;
using MediaPi.Core.RestModels.Device;
using MediaPi.Core.Services.Interfaces;
using MediaPi.Core.Services.Models;
using Microsoft.AspNetCore.Mvc;

namespace MediaPi.Core.Controllers;

public partial class DevicesController
{
    [HttpPost("{id}/screenshot")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(FileContentResult))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status500InternalServerError, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status502BadGateway, Type = typeof(ErrMessage))]
    public async Task<IActionResult> CreateScreenshot(int id, CancellationToken ct = default)
    {
        var (device, error) = await GetDeviceForServiceAsync(id, ct);
        if (error != null) return error;

        var targetDevice = device!;

        DeviceScreenshotResult deviceScreenshot;
        try
        {
            deviceScreenshot = await mediaPiAgentClient.CreateScreenshotAsync(targetDevice, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при создании фотографии для устройства {DeviceId}", id);
            return _502Agent();
        }

        // Normalize filename and content-type before using them in storage or response headers.
        // IMediaPiAgentClient does not guarantee these values are sanitized, so we apply
        // defensive normalization regardless of the underlying implementation.
        var safeFilename = SanitizeScreenshotFilename(deviceScreenshot.Filename);
        var safeContentType = IsAllowedImageContentType(deviceScreenshot.ContentType) ? deviceScreenshot.ContentType : "image/jpeg";

        // Stream must remain open for the duration of SaveScreenshotAsync; it is disposed
        // at the end of this method scope, after the save call has completed.
        await using var stream = new MemoryStream(deviceScreenshot.Content);
        var formFile = new FormFile(stream, 0, deviceScreenshot.Content.Length, "file", safeFilename)
        {
            Headers = new HeaderDictionary(),
            ContentType = safeContentType
        };

        ScreenshotSaveResult saveResult;
        try
        {
            saveResult = await screenshotStorageService.SaveScreenshotAsync(formFile, Path.GetFileNameWithoutExtension(safeFilename), ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при сохранении фотографии для устройства {DeviceId}", id);
            return _500ScreenshotPersistence();
        }

        var screenshot = new Screenshot
        {
            Filename = saveResult.Filename,
            OriginalFilename = saveResult.OriginalFilename,
            FileSizeBytes = saveResult.FileSizeBytes,
            TimeCreated = saveResult.TimeCreated,
            DeviceId = id
        };

        try
        {
            _db.Screenshots.Add(screenshot);
            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при сохранении фотографии для устройства {DeviceId}", id);
            try
            {
                await screenshotStorageService.DeleteScreenshotAsync(saveResult.Filename, ct);
            }
            catch (Exception deleteEx)
            {
                logger.LogError(deleteEx, "Не удалось удалить фотографию {Filename}", saveResult.Filename);
            }
            return _500ScreenshotPersistence();
        }

        return File(deviceScreenshot.Content, safeContentType, safeFilename);
    }

    private static readonly HashSet<char> _unsafeFileNameChars = new(
        Path.GetInvalidFileNameChars().Concat(new[] { '\\', '/', ':', '*', '?', '"', '<', '>', '|' }));

    private static string SanitizeScreenshotFilename(string? filename)
    {
        if (string.IsNullOrWhiteSpace(filename))
            return "screenshot.jpg";

        var name = Path.GetFileName(filename.Replace('\\', '/'));
        var result = new System.Text.StringBuilder(name.Length);
        foreach (var c in name)
        {
            result.Append(char.IsControl(c) || _unsafeFileNameChars.Contains(c) ? '_' : c);
        }
        var safe = result.ToString().Trim();
        return string.IsNullOrWhiteSpace(safe) ? "screenshot.jpg" : safe;
    }

    private static bool IsAllowedImageContentType(string? contentType) =>
        !string.IsNullOrWhiteSpace(contentType) &&
        contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);

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
    public async Task<ActionResult<MediaPiMenuCommandResponse>> UpdateConfiguration(int id, [FromBody] ConfigurationSettingsDto payload, CancellationToken ct = default)
    {
        var (device, error) = await GetDeviceForServiceAsync(id, ct);
        if (error != null) return error;

        var targetDevice = device!;

        try
        {
            var updateResponse = await mediaPiAgentClient2.UpdateConfigurationAsync(targetDevice, payload, ct);
            if (!updateResponse.Ok)
            {
                logger.LogWarning("Не удалось сохранить конфигурацию устройства {DeviceId}: {Error}", id, updateResponse.ErrMsg ?? "неизвестная ошибка");
                return _502Agent(updateResponse.ErrMsg);
            }

            // If update succeeded, request the device to reload system configuration
            try
            {
                var reloadResponse = await mediaPiAgentClient2.ReloadSystemAsync(targetDevice, ct);
                if (!reloadResponse.Ok)
                {
                    logger.LogWarning("Не удалось применить конфигурацию устройства {DeviceId}: {Error}", id, reloadResponse.ErrMsg ?? "неизвестная ошибка");
                    return _502Agent(reloadResponse.ErrMsg);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Ошибка при выполнении операции reload system для устройства {DeviceId}", id);
                return _502Agent();
            }

            return Ok(updateResponse);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при выполнении операции update configuration для устройства {DeviceId}", id);
            return _502Agent();
        }
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

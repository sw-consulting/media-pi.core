// Copyright (c) 2025 sw.consulting
// This file is a part of Media Pi backend

using MediaPi.Core.Models;
using MediaPi.Core.RestModels.Device;
using MediaPi.Core.Services.Models;

namespace MediaPi.Core.Services.Interfaces;

public interface IMediaPiAgentClient2
{
    Task<MediaPiMenuCommandResponse> StopPlaybackAsync(Device device, CancellationToken cancellationToken = default);
    Task<MediaPiMenuCommandResponse> StartPlaybackAsync(Device device, CancellationToken cancellationToken = default);
    Task<MediaPiMenuCommandResponse> StartPlaylistUploadAsync(Device device, CancellationToken cancellationToken = default);
    Task<MediaPiMenuCommandResponse> StopPlaylistUploadAsync(Device device, CancellationToken cancellationToken = default);
    Task<MediaPiMenuCommandResponse> StartVideoUploadAsync(Device device, CancellationToken cancellationToken = default);
    Task<MediaPiMenuCommandResponse> StopVideoUploadAsync(Device device, CancellationToken cancellationToken = default);
    Task<MediaPiMenuDataResponse<ServiceStatusDto>> GetServiceStatusAsync(Device device, CancellationToken cancellationToken = default);
    Task<MediaPiMenuDataResponse<ConfigurationSettingsDto>> GetConfigurationAsync(Device device, CancellationToken cancellationToken = default);
    Task<MediaPiMenuCommandResponse> UpdateConfigurationAsync(Device device, ConfigurationSettingsDto payload, CancellationToken cancellationToken = default);
    Task<MediaPiMenuCommandResponse> ReloadSystemAsync(Device device, CancellationToken cancellationToken = default);
    Task<MediaPiMenuCommandResponse> RebootSystemAsync(Device device, CancellationToken cancellationToken = default);
    Task<MediaPiMenuCommandResponse> ShutdownSystemAsync(Device device, CancellationToken cancellationToken = default);
}

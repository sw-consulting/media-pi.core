// Copyright (c) 2025 sw.consulting
// This file is a part of Media Pi backend

using MediaPi.Core.Models;
using MediaPi.Core.Services.Models;

namespace MediaPi.Core.Services.Interfaces;

public interface IMediaPiAgentClient2
{
    Task<MediaPiMenuCommandResponse> StopPlaybackAsync(Device device, CancellationToken cancellationToken = default);
    Task<MediaPiMenuCommandResponse> StartPlaybackAsync(Device device, CancellationToken cancellationToken = default);
    Task<MediaPiMenuDataResponse> CheckStorageAsync(Device device, CancellationToken cancellationToken = default);
    Task<MediaPiMenuDataResponse> GetPlaylistSettingsAsync(Device device, CancellationToken cancellationToken = default);
    Task<MediaPiMenuCommandResponse> UpdatePlaylistSettingsAsync<TPayload>(Device device, TPayload payload, CancellationToken cancellationToken = default);
    Task<MediaPiMenuCommandResponse> StartPlaylistUploadAsync(Device device, CancellationToken cancellationToken = default);
    Task<MediaPiMenuCommandResponse> StopPlaylistUploadAsync(Device device, CancellationToken cancellationToken = default);
    Task<MediaPiMenuDataResponse> GetScheduleAsync(Device device, CancellationToken cancellationToken = default);
    Task<MediaPiMenuCommandResponse> UpdateScheduleAsync<TPayload>(Device device, TPayload payload, CancellationToken cancellationToken = default);
    Task<MediaPiMenuDataResponse> GetAudioSettingsAsync(Device device, CancellationToken cancellationToken = default);
    Task<MediaPiMenuCommandResponse> UpdateAudioSettingsAsync<TPayload>(Device device, TPayload payload, CancellationToken cancellationToken = default);
    Task<MediaPiMenuCommandResponse> ReloadSystemAsync(Device device, CancellationToken cancellationToken = default);
    Task<MediaPiMenuCommandResponse> RebootSystemAsync(Device device, CancellationToken cancellationToken = default);
    Task<MediaPiMenuCommandResponse> ShutdownSystemAsync(Device device, CancellationToken cancellationToken = default);
}

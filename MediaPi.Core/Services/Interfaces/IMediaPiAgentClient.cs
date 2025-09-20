// Copyright (c) 2025 sw.consulting
// This file is a part of Media Pi backend

using MediaPi.Core.Models;
using MediaPi.Core.Services.Models;

namespace MediaPi.Core.Services.Interfaces;

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

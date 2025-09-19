// Copyright (c) 2025 sw.consulting
// This file is a part of Media Pi backend

using MediaPi.Core.RestModels;
using MediaPi.Core.Services.Models;

namespace MediaPi.Core.Services;

public interface IDeviceMonitoringService
{
    IReadOnlyDictionary<int, DeviceStatusSnapshot> Snapshot { get; }
    bool TryGetStatus(int deviceId, out DeviceStatusSnapshot? status);
    bool TryGetStatusItem(int deviceId, out DeviceStatusItem? status);
    Task<DeviceStatusSnapshot?> Test(int deviceId, CancellationToken token = default);
    IAsyncEnumerable<DeviceStatusEvent> Subscribe(CancellationToken token = default);
}

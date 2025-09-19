// Copyright (c) 2025 sw.consulting
// This file is a part of Media Pi backend

using MediaPi.Core.Models;

namespace MediaPi.Core.Services
{
    public class DeviceEventsService
    {
        public event Action<Device>? DeviceCreated;
        public event Action<Device>? DeviceUpdated;
        public event Action<int>? DeviceDeleted;

        public void OnDeviceCreated(Device device)
        {
            DeviceCreated?.Invoke(device);
        }

        public void OnDeviceUpdated(Device device)
        {
            DeviceUpdated?.Invoke(device);
        }

        public void OnDeviceDeleted(int deviceId)
        {
            DeviceDeleted?.Invoke(deviceId);
        }
    }
}

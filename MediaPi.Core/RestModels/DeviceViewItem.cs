// Copyright (c) 2025 sw.consulting
// This file is a part of Media Pi backend

using System.Text.Json;

using MediaPi.Core.Models;
using MediaPi.Core.Settings;
using MediaPi.Core.Services.Models;

namespace MediaPi.Core.RestModels;

public class DeviceViewItem(Device device, DeviceStatusItem? status)
{
    public int Id { get; set; } = device.Id;
    public string Name { get; set; } = device.Name;
    public string IpAddress { get; set; } = device.IpAddress;
    public string SshUser { get; set; } = device.SshUser;
    public int? AccountId { get; set; } = device.AccountId;
    public int? DeviceGroupId { get; set; } = device.DeviceGroupId;
    public DeviceStatusItem? DeviceStatus { get; set; } = status;

    public override string ToString()
    {
        return JsonSerializer.Serialize(this, JOptions.DefaultOptions);
    }
}

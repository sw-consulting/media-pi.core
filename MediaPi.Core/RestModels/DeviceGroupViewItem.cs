// Copyright (c) 2025 sw.consulting
// This file is a part of Media Pi backend

using System.Text.Json;

using MediaPi.Core.Models;
using MediaPi.Core.Settings;

namespace MediaPi.Core.RestModels;

public class DeviceGroupViewItem(DeviceGroup group)
{
    public int Id { get; set; } = group.Id;
    public string Name { get; set; } = group.Name;
    public int AccountId { get; set; } = group.AccountId;

    public override string ToString()
    {
        return JsonSerializer.Serialize(this, JOptions.DefaultOptions);
    }
}

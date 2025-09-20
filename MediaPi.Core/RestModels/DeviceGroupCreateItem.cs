// Copyright (c) 2025 sw.consulting
// This file is a part of Media Pi backend

using System.Text.Json;

using MediaPi.Core.Settings;

namespace MediaPi.Core.RestModels;

public class DeviceGroupCreateItem
{
    public string Name { get; set; } = string.Empty;
    public int AccountId { get; set; }

    public override string ToString()
    {
        return JsonSerializer.Serialize(this, JOptions.DefaultOptions);
    }
}

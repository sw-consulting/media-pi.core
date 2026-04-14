// Copyright (C) 2025-2026 sw.consulting
// This file is a part of Media Pi backend

using System.Text.Json;

using MediaPi.Core.Settings;

namespace MediaPi.Core.RestModels;

public class DeviceGroupUpdateItem
{
    public string? Name { get; set; }
    public List<PlaylistDeviceGroupItemDto>? Playlists { get; set; }

    public override string ToString()
    {
        return JsonSerializer.Serialize(this, JOptions.DefaultOptions);
    }
}

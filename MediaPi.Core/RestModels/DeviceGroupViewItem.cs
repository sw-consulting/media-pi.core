// Copyright (c) 2025 sw.consulting
// This file is a part of Media Pi backend

using System.Text.Json;

using MediaPi.Core.Models;
using MediaPi.Core.Settings;

namespace MediaPi.Core.RestModels;

public class DeviceGroupViewItem
{
    public int Id { get; set; }
    public string Name { get; set; }
    public int AccountId { get; set; }
    public List<PlaylistDeviceGroupItemDto> PlayLists { get; set; }
    public override string ToString()
    {
        return JsonSerializer.Serialize(this, JOptions.DefaultOptions);
    }

    public DeviceGroupViewItem(DeviceGroup group)
    {
        Id = group.Id;
        Name = group.Name;
        AccountId = group.AccountId;
        PlayLists = [.. group.PlaylistsDeviceGroup
            .OrderBy(pdg => (pdg.Id))
            .Select((pdg => new PlaylistDeviceGroupItemDto { PlaylistId = pdg.PlaylistId, Play = pdg.Play }))];
    }
}


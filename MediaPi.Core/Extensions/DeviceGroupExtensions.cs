// Copyright (c) 2025 sw.consulting
// This file is a part of Media Pi backend

using MediaPi.Core.Models;
using MediaPi.Core.RestModels;

namespace MediaPi.Core.Extensions;

public static class DeviceGroupExtensions
{
    public static DeviceGroupViewItem ToViewItem(this DeviceGroup group) => new(group);

    public static void UpdateFrom(this DeviceGroup group, DeviceGroupUpdateItem item)
    {
        if (item.Name != null) group.Name = item.Name;
    }
}

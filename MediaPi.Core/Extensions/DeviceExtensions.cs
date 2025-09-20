// Copyright (c) 2025 sw.consulting
// This file is a part of Media Pi backend

using MediaPi.Core.Models;
using MediaPi.Core.RestModels;

namespace MediaPi.Core.Extensions;

public static class DeviceExtensions
{
    public static DeviceViewItem ToViewItem(this Device device, DeviceStatusItem? status) => new(device, status);

    public static void UpdateFrom(this Device device, DeviceUpdateItem item)
    {
        if (item.Name != null) device.Name = item.Name;
        
        if (item.Port.HasValue) device.Port = item.Port.Value;
       
        if (item.AccountId.HasValue) 
        {
            int? newAccountId = item.AccountId.Value == 0 ? null : item.AccountId.Value;
            
            if (device.AccountId != newAccountId)
            {
                device.DeviceGroupId = null;
            }
            
            device.AccountId = newAccountId;
        }
        
        if (item.DeviceGroupId.HasValue) 
        {
            device.DeviceGroupId = item.DeviceGroupId.Value == 0 ? null : item.DeviceGroupId.Value;
        }
    }

    public static void AssignGroupFrom(this Device device, Reference item)
    {
        device.DeviceGroupId = item.Id == 0 ? null : item.Id;
    }

    public static void AssignAccountFrom(this Device device, Reference item)
    {
        int? newAccountId = item.Id == 0 ? null : item.Id;
        
        if (device.AccountId != newAccountId)
        {
            device.DeviceGroupId = null;
        }
        
        device.AccountId = newAccountId;
    }
}

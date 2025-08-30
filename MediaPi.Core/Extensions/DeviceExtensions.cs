// MIT License
//
// Copyright (c) 2025 Maxim [maxirmx] Samsonov (www.sw.consulting)
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using MediaPi.Core.Models;
using MediaPi.Core.RestModels;
using MediaPi.Core.Utils;

namespace MediaPi.Core.Extensions;

public static class DeviceExtensions
{
    public static DeviceViewItem ToViewItem(this Device device, DeviceStatusItem? status) => new(device, status);

    public static void UpdateFrom(this Device device, DeviceUpdateItem item)
    {
        if (item.Name != null) device.Name = item.Name;
        
        if (item.PublicKeyOpenSsh != null) 
        {
            device.PublicKeyOpenSsh = item.PublicKeyOpenSsh;
            
            // Recalculate PiDeviceId when SSH key changes
            try
            {
                device.PiDeviceId = string.IsNullOrWhiteSpace(item.PublicKeyOpenSsh) 
                    ? KeyFingerprint.GenerateRandomDeviceId()
                    : KeyFingerprint.ComputeDeviceIdFromOpenSshKey(item.PublicKeyOpenSsh);
            }
            catch (ArgumentException)
            {
                // If SSH key is invalid, generate a random device ID
                device.PiDeviceId = KeyFingerprint.GenerateRandomDeviceId();
            }
        }
        
        if (item.SshUser != null) device.SshUser = item.SshUser;
        
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


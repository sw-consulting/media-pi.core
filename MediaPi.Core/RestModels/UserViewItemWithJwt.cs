// Copyright (c) 2025 sw.consulting
// This file is a part of Media Pi backend

using System.Text.Json;

using MediaPi.Core.Models;
using MediaPi.Core.Settings;

namespace MediaPi.Core.RestModels;

public class UserViewItemWithJWT(User user) : UserViewItem(user)
{
    public string Token { get; set; } = "";
    public override string ToString()
    {
        return JsonSerializer.Serialize(this, JOptions.DefaultOptions);
    }
}

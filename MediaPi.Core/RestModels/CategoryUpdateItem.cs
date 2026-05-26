// Copyright (C) 2025-2026 sw.consulting
// This file is a part of Media Pi backend

using System.Text.Json;
using MediaPi.Core.Settings;

namespace MediaPi.Core.RestModels;

public class CategoryUpdateItem
{
    public string? Title { get; set; }
    public bool? Free { get; set; }

    public override string ToString()
    {
        return JsonSerializer.Serialize(this, JOptions.DefaultOptions);
    }
}

// Copyright (C) 2025-2026 sw.consulting
// This file is a part of Media Pi backend

using System.Text.Json;
using MediaPi.Core.Settings;

namespace MediaPi.Core.RestModels;

public class CategoryCreateItem
{
    public string Title { get; set; } = string.Empty;
    public bool Free { get; set; } = true;

    public override string ToString()
    {
        return JsonSerializer.Serialize(this, JOptions.DefaultOptions);
    }
}

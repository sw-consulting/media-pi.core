// Copyright (C) 2025-2026 sw.consulting
// This file is a part of Media Pi backend

using System.Text.Json;
using MediaPi.Core.Models;
using MediaPi.Core.Settings;

namespace MediaPi.Core.RestModels;

public class CategoryViewItem(Category category)
{
    public int Id { get; init; } = category.Id;
    public string Title { get; init; } = category.Title;
    public bool Free { get; init; } = category.Free;

    public override string ToString()
    {
        return JsonSerializer.Serialize(this, JOptions.DefaultOptions);
    }
}

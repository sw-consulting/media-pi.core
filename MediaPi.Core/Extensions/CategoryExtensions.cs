// Copyright (C) 2025-2026 sw.consulting
// This file is a part of Media Pi backend

using MediaPi.Core.Models;
using MediaPi.Core.RestModels;

namespace MediaPi.Core.Extensions;

public static class CategoryExtensions
{
    public static CategoryViewItem ToViewItem(this Category category) => new(category);

    public static void UpdateFrom(this Category category, CategoryUpdateItem item)
    {
        if (item.Title != null) category.Title = item.Title;
        if (item.Free != null) category.Free = item.Free.Value;
    }
}

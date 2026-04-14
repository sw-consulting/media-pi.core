// Copyright (C) 2025-2026 sw.consulting
// This file is a part of Media Pi backend

using MediaPi.Core.Models;
using MediaPi.Core.RestModels;

namespace MediaPi.Core.Extensions;

public static class VideoExtensions
{
    public static VideoViewItem ToViewItem(this Video video) => new(video);
}

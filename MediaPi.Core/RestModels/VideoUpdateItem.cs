// Copyright (c) 2025 sw.consulting
// This file is a part of Media Pi backend

using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace MediaPi.Core.RestModels;

public class VideoUpdateItem
{
    [Required]
    public string Title { get; set; } = string.Empty;

    public List<int>? PlaylistIds { get; set; }
}

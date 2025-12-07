// Copyright (c) 2025 sw.consulting
// This file is a part of Media Pi backend

using System.Text.Json;

using MediaPi.Core.Models;
using MediaPi.Core.Settings;

namespace MediaPi.Core.RestModels;

public class VideoViewItem(Video video)
{
    public int Id { get; set; } = video.Id;
    public string Title { get; set; } = video.Title;
    public string Filename { get; set; } = video.Filename;
    public string OriginalFilename { get; set; } = video.OriginalFilename;
    public uint FileSizeBytes { get; set; } = video.FileSizeBytes;
    public uint? DurationSeconds { get; set; } = video.DurationSeconds;
    public int AccountId { get; set; } = video.AccountId;

    public override string ToString()
    {
        return JsonSerializer.Serialize(this, JOptions.DefaultOptions);
    }
}

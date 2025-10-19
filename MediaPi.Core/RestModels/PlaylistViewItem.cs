// Copyright (c) 2025 sw.consulting
// This file is a part of Media Pi backend

using System.Text.Json;

using MediaPi.Core.Models;
using MediaPi.Core.Settings;

namespace MediaPi.Core.RestModels;

public class PlaylistViewItem(Playlist playlist)
{
    public int Id { get; set; } = playlist.Id;
    public string Title { get; set; } = playlist.Title;
    public string Filename { get; set; } = playlist.Filename;
    public int AccountId { get; set; } = playlist.AccountId;
    public IEnumerable<int> VideoIds { get; set; } = [.. playlist.VideoPlaylists.Select(vp => vp.VideoId)];
    public override string ToString()
    {
        return JsonSerializer.Serialize(this, JOptions.DefaultOptions);
    }
}

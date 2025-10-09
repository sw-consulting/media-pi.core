// Copyright (c) 2025 sw.consulting
// This file is a part of Media Pi backend

using MediaPi.Core.Models;
using MediaPi.Core.RestModels;

namespace MediaPi.Core.Extensions;

public static class PlaylistExtensions
{
    public static PlaylistViewItem ToViewItem(this Playlist playlist) => new(playlist);

    public static void UpdateFrom(this Playlist playlist, PlaylistUpdateItem item)
    {
        playlist.Title = item.Title;
        playlist.Filename = item.Filename;
    }
}

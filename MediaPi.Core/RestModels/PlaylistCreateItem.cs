// Copyright (c) 2025 sw.consulting
// This file is a part of Media Pi backend

using System.Collections.Generic;

namespace MediaPi.Core.RestModels
{
    public class PlaylistCreateItem
    {
        public required string Title { get; set; }
        public required string Filename { get; set; }
        public int AccountId { get; set; }
        public List<int> VideoIds { get; set; } = [];
    }
}

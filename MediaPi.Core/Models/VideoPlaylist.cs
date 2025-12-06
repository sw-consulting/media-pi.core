// Copyright (c) 2025 sw.consulting
// This file is a part of Media Pi backend

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MediaPi.Core.Models
{
    [Table("video_playlists")]
    public class VideoPlaylist
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("video_id")]
        public int VideoId { get; set; }
        public Video Video { get; set; } = null!;

        [Column("playlist_id")]
        public int PlaylistId { get; set; }
        public Playlist Playlist { get; set; } = null!;

        [Column("position")]
        public int Position { get; set; }
    }
}

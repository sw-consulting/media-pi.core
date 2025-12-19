// Copyright (c) 2025 sw.consulting
// This file is a part of Media Pi backend

using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace MediaPi.Core.Models
{
    [Index(nameof(AccountId), nameof(Filename), IsUnique = true)]
    [Table("playlists")]
    public class Playlist
    {
        [Column("id")]
        public int Id { get; set; }

        [Column("title")]
        public required string Title { get; set; }

        [Column("filename")]
        public required string Filename { get; set; }

        [Column("account_id")]
        public int AccountId { get; set; }
        public Account Account { get; set; } = null!;
        public ICollection<VideoPlaylist> VideoPlaylists { get; set; } = [];
    }
}

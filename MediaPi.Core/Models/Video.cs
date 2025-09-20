// Copyright (c) 2025 sw.consulting
// This file is a part of Media Pi backend

using System.ComponentModel.DataAnnotations.Schema;

namespace MediaPi.Core.Models
{
    [Table("videos")]
    public class Video
    {
        [Column("id")]
        public int Id { get; set; }

        [Column("title")]
        public required string Title { get; set; }

        [Column("filename")]
        public required string Filename { get; set; }

        [Column("category_id")]
        public int? CategoryId { get; set; }
        public Category? Category { get; set; }

        [Column("account_id")]
        public int AccountId { get; set; }
        public Account Account { get; set; } = null!;

        public ICollection<VideoPlaylist> VideoPlaylists { get; set; } = [];
    }
}

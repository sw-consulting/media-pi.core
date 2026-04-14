// Copyright (C) 2025-2026 sw.consulting
// This file is a part of Media Pi backend

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace MediaPi.Core.Models
{
    [Index(nameof(Filename), IsUnique = true)]
    [Table("videos")]
    public class Video
    {
        [Column("id")]
        public int Id { get; set; }

        [Column("title")]
        public required string Title { get; set; }

        [Column("filename")]
        public required string Filename { get; set; }

        [Column("original_filename")]
        public required string OriginalFilename { get; init; }

        /// <summary>
        /// File size in bytes. Limited to 4GB (uint.MaxValue = 4,294,967,295 bytes).
        /// Database stores as bigint but application enforces uint range constraints.
        /// EF Core automatically maps uint to PostgreSQL bigint for safety.
        /// </summary>
        [Column("file_size_bytes")]
        [Range(0, uint.MaxValue, ErrorMessage = "File size must be between 0 and 4,294,967,295 bytes (4GB limit)")]
        public required uint FileSizeBytes { get; init; }

        /// <summary>
        /// Video duration in seconds. Limited to ~136 years (uint.MaxValue = 4,294,967,295 seconds).
        /// Database stores as bigint but application enforces uint range constraints.
        /// EF Core automatically maps uint to PostgreSQL bigint for safety.
        /// </summary>
        [Column("duration_seconds")]
        [Range(0, uint.MaxValue, ErrorMessage = "Duration must be between 0 and 4,294,967,295 seconds")]
        public uint? DurationSeconds { get; set; }

        // Store SHA-256 as fixed-length 64-character hex string (Postgres CHAR(64))
        [Column("sha256", TypeName = "char(64)")]
        [StringLength(64)]
        public string? Sha256 { get; set; }

        [Column("category_id")]
        public int? CategoryId { get; set; }
        public Category? Category { get; set; }

        [Column("account_id")]
        public int? AccountId { get; set; }
        public Account? Account { get; set; }

        public ICollection<VideoPlaylist> VideoPlaylists { get; set; } = [];
    }
}

// Copyright (c) 2025 sw.consulting
// This file is a part of Media Pi backend

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace MediaPi.Core.Models
{
    [Index(nameof(Filename), IsUnique = true)]
    [Table("screenshots")]
    public class Screenshot
    {
        [Column("id")]
        public int Id { get; set; }

        [Column("filename")]
        public required string Filename { get; set; }

        [Column("original_filename")]
        public required string OriginalFilename { get; init; }

        /// <summary>
        /// File size in bytes. Limited to 4GB (uint.MaxValue = 4,294,967,295 bytes).
        /// </summary>
        [Column("file_size_bytes")]
        [Range(0, uint.MaxValue, ErrorMessage = "File size must be between 0 and 4,294,967,295 bytes (4GB limit)")]
        public required uint FileSizeBytes { get; init; }

        [Column("time_created")]
        public DateTime TimeCreated { get; set; }

        [Column("device_id")]
        public int DeviceId { get; set; }
        public Device Device { get; set; } = null!;
    }
}

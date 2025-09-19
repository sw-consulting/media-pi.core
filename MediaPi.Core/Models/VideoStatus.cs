// Copyright (c) 2025 sw.consulting
// This file is a part of Media Pi backend

using System.ComponentModel.DataAnnotations.Schema;

namespace MediaPi.Core.Models
{
    [Table("video_statuses")]
    public class VideoStatus
    {
        [Column("id")]
        public StatusConstants Id { get; set; }

        [Column("name")]
        public required string Name { get; set; }

    }
}

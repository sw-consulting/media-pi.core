// Copyright (c) 2025 sw.consulting
// This file is a part of Media Pi backend

using System.ComponentModel.DataAnnotations.Schema;

namespace MediaPi.Core.Models
{
    [Table("screenshots")]
    public class Screenshot
    {
        [Column("id")]
        public int Id { get; set; }

        [Column("timestamp")]
        public DateTime Timestamp { get; set; }

        [Column("device_id")]
        public int DeviceId { get; set; }
        public Device Device { get; set; } = null!;
    }
}

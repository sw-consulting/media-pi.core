// Copyright (C) 2025-2026 sw.consulting
// This file is a part of Media Pi backend

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MediaPi.Core.Models
{
    [Table("playlist_device_group")]
    public class PlaylistDeviceGroup
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("playlist_id")]
        public int PlaylistId { get; set; }
        public Playlist Playlist { get; set; } = null!;

        [Column("device_group_id")]
        public int DeviceGroupId { get; set; }
        public DeviceGroup DeviceGroup { get; set; } = null!;

        [Column("play")]
        public bool Play { get; set; }
    }
}

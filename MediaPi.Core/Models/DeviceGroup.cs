// Copyright (c) 2025 sw.consulting
// This file is a part of Media Pi backend

using System.ComponentModel.DataAnnotations.Schema;

namespace MediaPi.Core.Models
{
    [Table("device_groups")]
    public class DeviceGroup
    {
        [Column("id")]
        public int Id { get; set; }

        [Column("name")]
        public required string Name { get; set; }

        [Column("account_id")]
        public int AccountId { get; set; }
        public Account Account { get; set; } = null!;

        public ICollection<Device> Devices { get; set; } = [];
    }
}

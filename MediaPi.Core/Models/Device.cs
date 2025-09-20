// Copyright (c) 2025 sw.consulting
// This file is a part of Media Pi backend

using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace MediaPi.Core.Models
{
    [Table("devices")]
    [Index(nameof(IpAddress), IsUnique = true)]
    public class Device
    {
        [Column("id")]
        public int Id { get; set; }

        [Column("name")]
        public required string Name { get; set; }

        [Column("ip_address")]
        public required string IpAddress { get; set; }

        [Column("port")]
        public required string Port { get; set; }

        [Column("server_key")]
        public string ServerKey { get; set; } = string.Empty;


        [Column("account_id")]
        public int? AccountId { get; set; }
        public Account? Account { get; set; }

        [Column("device_group_id")]
        public int? DeviceGroupId { get; set; }
        public DeviceGroup? DeviceGroup { get; set; }

        public ICollection<Screenshot> Screenshots { get; set; } = [];

    }
}

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

        [Column("public_key_open_ssh")]
        public string PublicKeyOpenSsh { get; set; } = string.Empty;

        [Column("ssh_user")]
        public string SshUser { get; set; } = "pi";

        [Column("account_id")]
        public int? AccountId { get; set; }
        public Account? Account { get; set; }

        [Column("device_group_id")]
        public int? DeviceGroupId { get; set; }
        public DeviceGroup? DeviceGroup { get; set; }

        public ICollection<Screenshot> Screenshots { get; set; } = [];

    }
}

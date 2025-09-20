// Copyright (c) 2025 sw.consulting
// This file is a part of Media Pi backend

using System.ComponentModel.DataAnnotations.Schema;

namespace MediaPi.Core.Models
{
    [Table("accounts")]
    public class Account
    {
        [Column("id")]
        public int Id { get; set; }

        [Column("name")]
        public required string Name { get; set; }

        public ICollection<Device> Devices { get; set; } = [];
        public ICollection<DeviceGroup> DeviceGroups { get; set; } = [];
        public ICollection<Video> Videos { get; set; } = [];
        public ICollection<Playlist> Playlists { get; set; } = [];
        public ICollection<Subscription> Subscriptions { get; set; } = [];
        public ICollection<UserAccount> UserAccounts { get; set; } = [];
    }
}

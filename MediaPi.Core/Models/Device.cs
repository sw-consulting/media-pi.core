// MIT License
//
// Copyright (c) 2025 Maxim [maxirmx] Samsonov (www.sw.consulting)
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using System.ComponentModel.DataAnnotations.Schema;

namespace MediaPi.Core.Models
{
    [Table("devices")]
    public class Device
    {
        [Column("id")]
        public int Id { get; set; }

        [Column("name")]
        public required string Name { get; set; }

        [Column("ip_address")]
        public required string IpAddress { get; set; }

        [Column("pi_device_id")]
        public string PiDeviceId { get; set; } = string.Empty;

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

        [NotMapped]
        public string Alias => $"pi-{PiDeviceId}";

        [NotMapped]
        public string SocketPath => $"/run/mediapi/{PiDeviceId}.ssh.sock";

    }
}


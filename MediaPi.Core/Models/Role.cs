// Copyright (c) 2025 sw.consulting
// This file is a part of Media Pi backend

using System.ComponentModel.DataAnnotations.Schema;

namespace MediaPi.Core.Models
{
    [Table("roles")]
    public class Role
    {
        [Column("id")]
        public int Id { get; set; }

        [Column("role_id")]
        public UserRoleConstants RoleId { get; set; }

        [Column("name")]
        public required string Name { get; set; }

        public ICollection<UserRole> UserRoles { get; set; } = [];
    }
}

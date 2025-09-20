// Copyright (c) 2025 sw.consulting
// This file is a part of Media Pi backend

using System.ComponentModel.DataAnnotations.Schema;

namespace MediaPi.Core.Models
{
    [Table("users")]
    public class User
    {
        [Column("id")]
        public int Id { get; set; }

        [Column("first_name")]
        public string FirstName { get; set; } = "";

        [Column("last_name")]
        public string LastName { get; set; } = "";

        [Column("patronymic")]
        public string Patronymic { get; set; } = "";

        [Column("email")]
        public required string Email { get; set; }

        [Column("password")]
        public required string Password { get; set; }

        public ICollection<UserRole> UserRoles { get; set; } = [];
        public ICollection<UserAccount> UserAccounts { get; set; } = [];

        public bool HasAnyRole() => UserRoles.Count != 0;

        public bool HasRole(UserRoleConstants role)
        {
            return UserRoles.Any(ur => ur.Role!.RoleId == role);
        }

        public bool IsAdministrator() => HasRole(UserRoleConstants.SystemAdministrator);
        public bool IsManager() => HasRole(UserRoleConstants.AccountManager);
        public bool IsEngineer() => HasRole(UserRoleConstants.InstallationEngineer);

    }
}

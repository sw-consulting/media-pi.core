// Copyright (c) 2025 sw.consulting
// This file is a part of Media Pi backend

using System.ComponentModel.DataAnnotations.Schema;

namespace MediaPi.Core.Models
{
    [Table("user_accounts")]
    public class UserAccount
    {
        [Column("user_id")]
        public int UserId { get; set; }
        public User User { get; set; } = null!;

        [Column("account_id")]
        public int AccountId { get; set; }
        public Account Account { get; set; } = null!;
    }
}

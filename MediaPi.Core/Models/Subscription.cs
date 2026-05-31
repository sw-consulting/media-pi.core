// Copyright (C) 2025-2026 sw.consulting
// This file is a part of Media Pi backend

using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace MediaPi.Core.Models
{
    [Index(nameof(AccountId), nameof(CategoryId), IsUnique = true)]
    [Table("subscriptions")]
    public class Subscription
    {
        [Column("id")]
        public int Id { get; set; }

        [Column("start_time")]
        public DateTime StartTime { get; set; }

        [Column("end_time")]
        public DateTime EndTime { get; set; }

        [Column("account_id")]
        public int AccountId { get; set; }
        public Account Account { get; set; } = null!;

        [Column("category_id")]
        public int CategoryId { get; set; }
        public Category Category { get; set; } = null!;
    }
}

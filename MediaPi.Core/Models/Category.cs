// Copyright (c) 2025 sw.consulting
// This file is a part of Media Pi backend

using System.ComponentModel.DataAnnotations.Schema;

namespace MediaPi.Core.Models
{
    [Table("categories")]
    public class Category
    {
        [Column("id")]
        public int Id { get; set; }

        [Column("title")]
        public required string Title { get; set; }

        public ICollection<Video> Videos { get; set; } = [];
        public ICollection<Subscription> Subscriptions { get; set; } = [];
    }
}

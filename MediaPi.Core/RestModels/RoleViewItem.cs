// Copyright (C) 2025-2026 sw.consulting
// This file is a part of Media Pi backend

using MediaPi.Core.Models;

namespace MediaPi.Core.RestModels
{
    public class RoleViewItem
    {
        public int Id { get; set; }
        public UserRoleConstants RoleId { get; set; }
        public required string Name { get; set; }
    }
}

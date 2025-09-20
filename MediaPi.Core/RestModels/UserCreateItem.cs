// Copyright (c) 2025 sw.consulting
// This file is a part of Media Pi backend

using System.Text.Json;
using MediaPi.Core.Models;
using MediaPi.Core.Settings;

namespace MediaPi.Core.RestModels;

public class UserCreateItem
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Patronymic { get; set; }
    public string Email { get; set; } = "";
    public string Password { get; set; } = "";
    public List<UserRoleConstants> Roles { get; set; } = [];
    public List<int> AccountIds { get; set; } = [];
  
    public bool HasRole(UserRoleConstants roleConstant)
    {
        return Roles != null && Roles.Contains(roleConstant);
    }
    public override string ToString()
    {
        return JsonSerializer.Serialize(this, JOptions.DefaultOptions);
    }
}

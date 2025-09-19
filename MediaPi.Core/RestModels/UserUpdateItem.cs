// Copyright (c) 2025 sw.consulting
// This file is a part of Media Pi backend

using System.Text.Json;
using MediaPi.Core.Settings;
using MediaPi.Core.Models;

namespace MediaPi.Core.RestModels;

public class UserUpdateItem
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Patronymic { get; set; }
    public string? Email { get; set; }
    public string? Password { get; set; }
    public List<UserRoleConstants>? Roles { get; set; } = null;
    public List<int>? AccountIds { get; set; } = null;
    
    public override string ToString()
    {
        return JsonSerializer.Serialize(this, JOptions.DefaultOptions);
    }
    
    public bool HasRole(UserRoleConstants roleConstant)
    {
        return Roles != null && Roles.Contains(roleConstant);
    }

    public bool IsAdministrator() => HasRole(UserRoleConstants.SystemAdministrator);
}

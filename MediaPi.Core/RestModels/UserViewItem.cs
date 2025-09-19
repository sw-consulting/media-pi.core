// Copyright (c) 2025 sw.consulting
// This file is a part of Media Pi backend

using System.Text.Json;

using MediaPi.Core.Models;
using MediaPi.Core.Settings;

namespace MediaPi.Core.RestModels;

public class UserViewItem
{
    public int Id { get; set; }
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string Patronymic { get; set; } = "";
    public string Email { get; set; } = "";
    public List<UserRoleConstants> Roles { get; set; } = [];
    public List<int> AccountIds { get; set; } = [];

    // Parameterless constructor for object initialization
    public UserViewItem() { }

    // Constructor with User parameter
    public UserViewItem(User user)
    {
        Id = user.Id;
        FirstName = user.FirstName;
        LastName = user.LastName;
        Patronymic = user.Patronymic;
        Email = user.Email;
        Roles = [.. user.UserRoles.Select(ur => ur.Role!.RoleId)];
        AccountIds = user.HasRole(UserRoleConstants.AccountManager)
            ? [.. user.UserAccounts
                .Where(ua => ua.UserId == user.Id)
                .Select(ua => ua.AccountId)]
            : [];
    }

    public override string ToString()
    {
        return JsonSerializer.Serialize(this, JOptions.DefaultOptions);
    }
}

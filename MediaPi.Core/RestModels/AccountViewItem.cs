// Copyright (c) 2025 sw.consulting
// This file is a part of Media Pi backend

using System.Text.Json;
using MediaPi.Core.Models;
using MediaPi.Core.Settings;

namespace MediaPi.Core.RestModels;

public class AccountViewItem(Account account)
{
    public int Id { get; set; } = account.Id;
    public string Name { get; set; } = account.Name;
    public List<int> UserIds { get; set; } = [];
    public override string ToString()
    {
        return JsonSerializer.Serialize(this, JOptions.DefaultOptions);
    }
}

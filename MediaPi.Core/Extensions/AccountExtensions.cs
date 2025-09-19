// Copyright (c) 2025 sw.consulting
// This file is a part of Media Pi backend

using MediaPi.Core.Models;
using MediaPi.Core.RestModels;

namespace MediaPi.Core.Extensions;

public static class AccountExtensions
{
    public static AccountViewItem ToViewItem(this Account account) => new(account);

    public static void UpdateFrom(this Account account, AccountUpdateItem item)
    {
        if (item.Name != null) account.Name = item.Name;
    }
}

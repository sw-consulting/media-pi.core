// Copyright (c) 2025 sw.consulting
// This file is a part of Media Pi backend

namespace MediaPi.Core.Settings;

public class AppSettings
{
    public string? Secret { get; set; } = null;
    public string? Token { get; set; } = null;
    public int JwtTokenExpirationDays { get; set; } = 7;
}

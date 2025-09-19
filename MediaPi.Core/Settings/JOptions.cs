// Copyright (c) 2025 sw.consulting
// This file is a part of Media Pi backend

using System.Text.Encodings.Web;
using System.Text.Json;

namespace MediaPi.Core.Settings;

public static class JOptions
{
    public static readonly JsonSerializerOptions DefaultOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static readonly JsonSerializerOptions StreamJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
}

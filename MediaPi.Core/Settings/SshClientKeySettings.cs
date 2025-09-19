// Copyright (c) 2025 sw.consulting
// This file is a part of Media Pi backend

namespace MediaPi.Core.Settings;

public sealed class SshClientKeySettings
{
    public string PrivateKeyPath { get; init; } = string.Empty; // Path on server filesystem (not stored in DB)
    public string PublicKeyPath  { get; init; } = string.Empty; // Public key path returned to registering devices
}

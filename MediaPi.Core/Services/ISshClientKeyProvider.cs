// Copyright (c) 2025 sw.consulting
// This file is a part of Media Pi backend

namespace MediaPi.Core.Services;

public interface ISshClientKeyProvider
{
    string GetPublicKey();
    string GetPrivateKeyPath();
}

// Copyright (C) 2025-2026 sw.consulting
// This file is a part of Media Pi backend

namespace MediaPi.Core.RestModels.Device;

public class DeviceApiResponse<T>
{
    public bool Ok { get; init; }
    public string? ErrMsg { get; init; }
    public T? Data { get; init; }
}

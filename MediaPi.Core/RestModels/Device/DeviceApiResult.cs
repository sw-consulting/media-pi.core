// Copyright (C) 2025-2026 sw.consulting
// This file is a part of Media Pi backend

using System.Net;

namespace MediaPi.Core.RestModels.Device;

public sealed record DeviceApiResult<T>(DeviceApiResponse<T> Response, HttpStatusCode StatusCode);

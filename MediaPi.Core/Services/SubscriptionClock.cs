// Copyright (C) 2025-2026 sw.consulting
// This file is a part of Media Pi backend

using MediaPi.Core.Services.Interfaces;

namespace MediaPi.Core.Services;

public class SubscriptionClock : ISubscriptionClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}

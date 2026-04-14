// Copyright (C) 2025-2026 sw.consulting
// This file is a part of Media Pi backend

using System;

namespace MediaPi.Core.Authorization;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class AuthorizeDeviceAttribute : Attribute
{
}

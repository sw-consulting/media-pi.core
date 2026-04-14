// Copyright (C) 2025-2026 sw.consulting
// This file is a part of Media Pi backend

namespace MediaPi.Core.RestModels;
public class Status
{
    public required string Msg { get; set; }
    public required string AppVersion { get; set; }
    public required string DbVersion { get; set; }

}

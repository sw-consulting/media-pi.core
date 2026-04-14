// Copyright (C) 2025-2026 sw.consulting
// This file is a part of Media Pi backend

namespace MediaPi.Core.RestModels;

public class ErrMessage
{
    public required string Msg { get; set; }
    public override string ToString()
    {
        return $"Error: \"{Msg}\"";
    }

}

// Copyright (C) 2025-2026 sw.consulting
// This file is a part of Media Pi backend

namespace MediaPi.Core.RestModels;

public class ErrMessage
{
    public required string Msg { get; set; }
    public string? Reason { get; set; }
    public string? OriginalFilename { get; set; }
    public int? AccountId { get; set; }
    public int? CategoryId { get; set; }

    public override string ToString()
    {
        return $"Error: \"{Msg}\"";
    }

}

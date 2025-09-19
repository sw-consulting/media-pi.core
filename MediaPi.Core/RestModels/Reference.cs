// Copyright (c) 2025 sw.consulting
// This file is a part of Media Pi backend

namespace MediaPi.Core.RestModels;
public class Reference
{
    public required int Id { get; set; }
    public override string ToString()
    {
        return $"Reference: {Id}";
    }
}

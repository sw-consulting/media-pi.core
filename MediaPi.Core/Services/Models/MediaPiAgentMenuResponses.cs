// Copyright (c) 2025 sw.consulting
// This file is a part of Media Pi backend

using System.Text.Json;
using System.Text.Json.Serialization;

namespace MediaPi.Core.Services.Models;

public record class MediaPiMenuDataResponse : MediaPiAgentResponse
{
    private static readonly JsonSerializerOptions DefaultSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    [JsonPropertyName("data")] public JsonElement Data { get; init; }

    [JsonIgnore]
    public bool HasData => Data.ValueKind is not JsonValueKind.Undefined and not JsonValueKind.Null;

    public T? DeserializeData<T>(JsonSerializerOptions? options = null)
    {
        if (!HasData)
        {
            return default;
        }

        return Data.Deserialize<T>(options ?? DefaultSerializerOptions);
    }
}

public record class MediaPiMenuDataResponse<T> : MediaPiAgentResponse
{
    public T? Data { get; init; }

    [JsonIgnore]
    public bool HasData => Data != null;

}

public sealed record class MediaPiMenuCommandResponse : MediaPiMenuDataResponse
{
    [JsonPropertyName("result")] public string? Result { get; init; }
}

using System.Text.Json;
using System.Text.Json.Serialization;

namespace MediaPi.Core.Services.Models
{
    public record class MediaPiAgentResponse
    {
        [JsonPropertyName("ok")] public bool Ok { get; init; }
        [JsonPropertyName("error")] public string? Error { get; init; }
    }

    public record class MediaPiAgentListResponse : MediaPiAgentResponse
    {
        [JsonPropertyName("units")] public IReadOnlyList<MediaPiAgentListUnit> Units { get; init; } = Array.Empty<MediaPiAgentListUnit>();
    }

    public record class MediaPiAgentListUnit
    {
        [JsonPropertyName("unit")] public string? Unit { get; init; }
        [JsonPropertyName("active")] public JsonElement Active { get; init; }
        [JsonIgnore] public string? ActiveState => Active.ValueKind == JsonValueKind.String ? Active.GetString() : null;
        [JsonPropertyName("sub")] public JsonElement Sub { get; init; }
        [JsonIgnore] public string? SubState => Sub.ValueKind == JsonValueKind.String ? Sub.GetString() : null;
        [JsonPropertyName("error")] public string? Error { get; init; }
    }

    public record class MediaPiAgentStatusResponse : MediaPiAgentResponse
    {
        [JsonPropertyName("unit")] public string? Unit { get; init; }
        [JsonPropertyName("active")] public JsonElement Active { get; init; }
        [JsonIgnore] public string? ActiveState => Active.ValueKind == JsonValueKind.String ? Active.GetString() : null;
        [JsonPropertyName("sub")] public JsonElement Sub { get; init; }
        [JsonIgnore] public string? SubState => Sub.ValueKind == JsonValueKind.String ? Sub.GetString() : null;
    }

    public record class MediaPiAgentUnitResultResponse : MediaPiAgentResponse
    {
        [JsonPropertyName("unit")] public string? Unit { get; init; }
        [JsonPropertyName("result")] public string? Result { get; init; }
    }

    public record class MediaPiAgentEnableResponse : MediaPiAgentResponse
    {
        [JsonPropertyName("unit")] public string? Unit { get; init; }
        [JsonPropertyName("enabled")] public bool? Enabled { get; init; }
    }
}

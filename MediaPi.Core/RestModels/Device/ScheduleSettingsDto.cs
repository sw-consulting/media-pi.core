// Copyright (C) 2025-2026 sw.consulting
// This file is a part of Media Pi backend

using System.Globalization;
using System.Text.Json.Serialization;

namespace MediaPi.Core.RestModels.Device
{
    public class RestTimePairDto
    {
        private string _stop = string.Empty;
        private string _start = string.Empty;

        [JsonPropertyName("stop")]
        public required string Stop
        {
            get => _stop;
            set
            {
                ValidateTimeMark(nameof(Stop), value);
                _stop = value;
            }
        }

        [JsonPropertyName("start")]
        public required string Start
        {
            get => _start;
            set
            {
                ValidateTimeMark(nameof(Start), value);
                _start = value;
            }
        }

        // Parameterless constructor to support deserialization
        public RestTimePairDto() { }

        // Convenience constructor that validates inputs
        public RestTimePairDto(string stop, string start)
        {
            Stop = stop;
            Start = start;
        }

        private static void ValidateTimeMark(string name, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException($"{name} must be a non-empty time in HH:mm format", name);

            // Strict parse for HH:mm (24-hour). Use InvariantCulture.
            if (!DateTime.TryParseExact(value, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
            {
                throw new ArgumentException($"{name} must be a valid time in HH:mm format (e.g. 23:59): '{value}'", name);
            }
        }
    }

    public class ScheduleSettingsDto
    {
        [JsonPropertyName("playlist")]
        public List<string> Playlist { get; set; } = [];

        [JsonPropertyName("video")]
        public List<string> Video { get; set; } = [];

        // "omitempty" behavior: omit when null
        [JsonPropertyName("rest")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<RestTimePairDto>? Rest
        {
            get => _rest;
            set
            {
                if (value is null)
                {
                    _rest = null;
                    return;
                }

                foreach (var item in value)
                {
                    if (item is null)
                        throw new ArgumentException("Rest list contains a null item", nameof(Rest));
                }

                _rest = value;
            }
        }

        // Add this private field to the ScheduleSettingsDto class to fix CS0103
        private List<RestTimePairDto>? _rest;

        // Parameterless constructor
        public ScheduleSettingsDto() { }
    }
}

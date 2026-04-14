// Copyright (C) 2025-2026 sw.consulting
// Test class to debug AudioSettingsDto deserialization

using System.Text.Json;
using MediaPi.Core.RestModels.Device;
using NUnit.Framework;

namespace MediaPi.Core.Tests.RestModels.Device;

[TestFixture]
public class AudioSettingsDtoTests
{
    [Test]
    public void AudioSettingsDto_DeserializesCorrectly()
    {
        // Simulate the exact JSON that the device returns
        var json = """{"output":"hdmi"}""";
        
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        };
        
        var result = JsonSerializer.Deserialize<AudioSettingsDto>(json, options);
        
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Output, Is.EqualTo("hdmi"));
    }

    [Test]
    public void AudioSettingsDto_DeserializesCorrectlyWithCapitalizedOutput()
    {
        // Test with different casing
        var json = """{"Output":"HDMI"}""";
        
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        };
        
        var result = JsonSerializer.Deserialize<AudioSettingsDto>(json, options);
        
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Output, Is.EqualTo("HDMI"));
    }

    [Test]
    public void AudioSettingsDto_SerializesCorrectly()
    {
        var audioSettings = new AudioSettingsDto { Output = "HDMI" };
        
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        
        var json = JsonSerializer.Serialize(audioSettings, options);
        
        Assert.That(json, Is.EqualTo("""{"output":"HDMI"}"""));
    }
}
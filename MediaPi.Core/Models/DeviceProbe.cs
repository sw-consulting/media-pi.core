// Copyright (c) 2025 sw.consulting
// This file is a part of Media Pi backend

using System.ComponentModel.DataAnnotations.Schema;

namespace MediaPi.Core.Models;

[Table("device_probes")]

public class DeviceProbe
{
    [Column("id")]
    public int Id { get; set; }

    [Column("device_id")]
    public int DeviceId { get; set; }
    public Device? Device { get; set; }
    [Column("timestamp")]
    public DateTime Timestamp { get; set; }

    [Column("is_online")]
    public bool IsOnline { get; set; }

    [Column("connect_latency")]
    public long ConnectLatencyMs { get; set; }

    [Column("total_latency")]
    public long TotalLatencyMs { get; set; }
}

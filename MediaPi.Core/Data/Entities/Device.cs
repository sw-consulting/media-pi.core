using System.ComponentModel.DataAnnotations;

namespace MediaPi.Core.Data.Entities;

public class Device
{
    [Key] public string Id { get; set; } = "";            // deviceId
    [Required] public string PublicKeyOpenSsh { get; set; } = "";
}

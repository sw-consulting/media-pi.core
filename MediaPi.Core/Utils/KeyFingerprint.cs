using System.Security.Cryptography;

namespace MediaPi.Core.Utils;

public static class KeyFingerprint
{
    public static string ComputeDeviceIdFromOpenSshKey(string openSshPubKey)
    {
        // Format: "<type> <base64> [comment]"
        var parts = openSshPubKey.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2) throw new ArgumentException("Invalid OpenSSH public key format");

        var b64 = parts[1];
        var keyBytes = Convert.FromBase64String(b64);

        using var sha = SHA256.Create();
        var digest = sha.ComputeHash(keyBytes);             // 32 bytes

        // Base64URL without padding, to mirror the Pi script deviceId
        var b64Digest = Convert.ToBase64String(digest).TrimEnd('=').Replace('+','-').Replace('/','_');
        return $"fp-{b64Digest}";
    }
}

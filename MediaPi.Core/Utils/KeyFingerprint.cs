// Developed by Maxim [maxirmx] Samsonov (www.sw.consulting)
// This file is a part of Media Pi backend application

using System.Security.Cryptography;

namespace MediaPi.Core.Utils;

public static class KeyFingerprint
{
    private static string Base64Url(ReadOnlySpan<byte> data)
    {
        return Convert.ToBase64String(data.ToArray()).TrimEnd('=')
            .Replace('+', '-').Replace('/', '_');
    }

    public static string ComputeDeviceIdFromOpenSshKey(string openSshPubKey)
    {
        // Format: "<type> <base64> [comment]"
        var parts = openSshPubKey.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2) throw new ArgumentException("Invalid OpenSSH public key format");

        var b64 = parts[1];
        var keyBytes = Convert.FromBase64String(b64);

        using var sha = SHA256.Create();
        var digest = sha.ComputeHash(keyBytes);             // 32 bytes

        var b64Digest = Base64Url(digest);
        return $"fp-{b64Digest}";
    }

    public static string GenerateRandomDeviceId()
    {
        Span<byte> random = stackalloc byte[32];
        RandomNumberGenerator.Fill(random);
        return $"fp-{Base64Url(random)}";
    }
}

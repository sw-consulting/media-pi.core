// Copyright (C) 2026 sw.consulting
// This file is a part of Media Pi backend

using MediaPi.Core.Settings;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace MediaPi.Core.Authorization;

public sealed record VideoPlaybackToken(string Token, DateTime ExpiresAt);

public interface IVideoPlaybackTokenService
{
    VideoPlaybackToken Generate(int userId, int videoId);
    int? Validate(string? token, int videoId);
}

public class VideoPlaybackTokenService : IVideoPlaybackTokenService
{
    private const string IdClaim = "id";
    private const string VideoIdClaim = "videoId";
    private const string PurposeClaim = "purpose";
    private const string PlaybackPurpose = "video-playback";
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromMinutes(60);

    private readonly AppSettings _appSettings;
    private readonly ILogger<VideoPlaybackTokenService> _logger;

    public VideoPlaybackTokenService(IOptions<AppSettings> appSettings, ILogger<VideoPlaybackTokenService> logger)
    {
        _appSettings = appSettings.Value;
        _logger = logger;

        if (string.IsNullOrEmpty(_appSettings.Secret))
        {
            _logger.LogError("JWT secret not configured");
            throw new Exception("JWT secret not configured");
        }
    }

    public VideoPlaybackToken Generate(int userId, int videoId)
    {
        var expiresAt = DateTime.UtcNow.Add(TokenLifetime);
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = CreateSigningKey();
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(IdClaim, userId.ToString()),
                new Claim(VideoIdClaim, videoId.ToString()),
                new Claim(PurposeClaim, PlaybackPurpose)
            }),
            Expires = expiresAt,
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256Signature)
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        return new VideoPlaybackToken(tokenHandler.WriteToken(token), expiresAt);
    }

    public int? Validate(string? token, int videoId)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;

        var tokenHandler = new JwtSecurityTokenHandler();
        try
        {
            tokenHandler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = CreateSigningKey(),
                ValidateIssuer = false,
                ValidateAudience = false,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            }, out var validatedToken);

            if (validatedToken is not JwtSecurityToken jwtToken) return null;

            var purpose = jwtToken.Claims.FirstOrDefault(x => x.Type == PurposeClaim)?.Value;
            if (!string.Equals(purpose, PlaybackPurpose, StringComparison.Ordinal)) return null;

            if (!int.TryParse(jwtToken.Claims.FirstOrDefault(x => x.Type == VideoIdClaim)?.Value, out var tokenVideoId)
                || tokenVideoId != videoId)
            {
                return null;
            }

            if (!int.TryParse(jwtToken.Claims.FirstOrDefault(x => x.Type == IdClaim)?.Value, out var userId)) return null;

            return userId;
        }
        catch (SecurityTokenExpiredException ex)
        {
            _logger.LogWarning(ex, "Video playback token expired");
            return null;
        }
        catch (SecurityTokenException ex)
        {
            _logger.LogWarning(ex, "Invalid video playback token");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating video playback token");
            return null;
        }
    }

    private SymmetricSecurityKey CreateSigningKey()
    {
        var key = SHA256.HashData(Encoding.UTF8.GetBytes(_appSettings.Secret!));
        return new SymmetricSecurityKey(key);
    }
}

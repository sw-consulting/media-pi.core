// Copyright (C) 2026 sw.consulting
// This file is a part of Media Pi backend

using MediaPi.Core.Authorization;
using MediaPi.Core.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Moq;
using NUnit.Framework;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace MediaPi.Core.Tests.Authorization;

[TestFixture]
public class VideoPlaybackTokenServiceTests
{
    private const string Secret = "video-playback-test-secret";

    [Test]
    public void Generate_Validate_ReturnsUserId()
    {
        var service = CreateService();

        var token = service.Generate(userId: 7, videoId: 11);
        var userId = service.Validate(token.Token, videoId: 11);

        Assert.That(userId, Is.EqualTo(7));
        Assert.That(token.ExpiresAt, Is.GreaterThan(DateTime.UtcNow.AddMinutes(59)));
        Assert.That(token.ExpiresAt, Is.LessThanOrEqualTo(DateTime.UtcNow.AddMinutes(60)));
    }

    [Test]
    public void Validate_WrongVideo_ReturnsNull()
    {
        var service = CreateService();
        var token = service.Generate(userId: 7, videoId: 11);

        var userId = service.Validate(token.Token, videoId: 12);

        Assert.That(userId, Is.Null);
    }

    [Test]
    public void Validate_WrongPurpose_ReturnsNull()
    {
        var service = CreateService();
        var token = CreateToken(userId: 7, videoId: 11, purpose: "other-purpose", expiresAt: DateTime.UtcNow.AddMinutes(60));

        var userId = service.Validate(token, videoId: 11);

        Assert.That(userId, Is.Null);
    }

    [Test]
    public void Validate_ExpiredToken_ReturnsNull()
    {
        var service = CreateService();
        var token = CreateToken(userId: 7, videoId: 11, purpose: "video-playback", expiresAt: DateTime.UtcNow.AddMinutes(-1));

        var userId = service.Validate(token, videoId: 11);

        Assert.That(userId, Is.Null);
    }

    private static VideoPlaybackTokenService CreateService()
    {
        return new VideoPlaybackTokenService(
            Options.Create(new AppSettings { Secret = Secret, JwtTokenExpirationDays = 7 }),
            Mock.Of<ILogger<VideoPlaybackTokenService>>());
    }

    private static string CreateToken(int userId, int videoId, string purpose, DateTime expiresAt)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = SHA256.HashData(Encoding.UTF8.GetBytes(Secret + ":video-playback"));
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim("id", userId.ToString()),
                new Claim("videoId", videoId.ToString()),
                new Claim("purpose", purpose)
            }),
            IssuedAt = DateTime.UtcNow.AddMinutes(-2),
            NotBefore = DateTime.UtcNow.AddMinutes(-2),
            Expires = expiresAt,
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
        };
        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }
}

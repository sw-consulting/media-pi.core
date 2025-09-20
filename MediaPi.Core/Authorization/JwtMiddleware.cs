// Copyright (c) 2025 sw.consulting
// This file is a part of Media Pi backend

namespace MediaPi.Core.Authorization;

public class JwtMiddleware
{
    private readonly RequestDelegate _next;

    public JwtMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task Invoke(HttpContext context, IJwtUtils jwtUtils)
    {
        var token = context.Request.Headers["Authorization"].FirstOrDefault()?.Split(" ").Last();
        var userId = jwtUtils.ValidateJwtToken(token);
        if (userId != null)
        {
            // attach User Id to context on successful jwt validation
            context.Items["UserId"] = userId.Value;
        }

        await _next(context);
    }
}

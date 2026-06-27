// Copyright (C) 2025-2026 sw.consulting
// This file is a part of Media Pi backend

using System.IO;

using MediaPi.Core.RestModels;

namespace MediaPi.Core.Middleware;

public class UploadExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<UploadExceptionMiddleware> _logger;

    public UploadExceptionMiddleware(RequestDelegate next, ILogger<UploadExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task Invoke(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex) when (IsVideoUploadTooLargeException(context, ex))
        {
            if (context.Response.HasStarted) throw;

            _logger.LogWarning(ex, "Uploaded video file exceeded the configured request size limit");
            context.Response.Clear();
            context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
            await context.Response.WriteAsJsonAsync(new ErrMessage
            {
                Msg = "Размер загружаемого файла превышает допустимый предел",
                Reason = ConflictReasons.VideoUploadTooLarge
            });
        }
    }

    private static bool IsVideoUploadTooLargeException(HttpContext context, Exception ex)
    {
        if (!context.Request.Path.Equals("/api/videos/upload", StringComparison.OrdinalIgnoreCase)) return false;

        for (Exception? current = ex; current != null; current = current.InnerException)
        {
            if (current is BadHttpRequestException badRequest
                && badRequest.StatusCode == StatusCodes.Status413PayloadTooLarge)
            {
                return true;
            }

            if (current is InvalidDataException invalidData
                && IsMultipartLimitMessage(invalidData.Message))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsMultipartLimitMessage(string message)
    {
        return message.Contains("Multipart body length limit", StringComparison.OrdinalIgnoreCase)
               || message.Contains("request body too large", StringComparison.OrdinalIgnoreCase)
               || message.Contains("exceeds the limit", StringComparison.OrdinalIgnoreCase);
    }
}

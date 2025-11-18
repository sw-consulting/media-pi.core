// Copyright (c) 2025 sw.consulting
// This file is a part of Media Pi backend

using System.Text;

namespace MediaPi.Core.Services;

public class LoggingHandler : DelegatingHandler
{
    private readonly ILogger<LoggingHandler> _logger;

    public LoggingHandler() : this(null)
    {
    }

    public LoggingHandler(ILogger<LoggingHandler>? logger)
    {
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<LoggingHandler>.Instance;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Log request
        await LogHttpMessage("Request", request.Method.ToString(), request.RequestUri?.ToString(), 
            request.Headers?.ToString(), request.Content).ConfigureAwait(false);

        // Send the request
        var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

        // Log response
        await LogHttpMessage("Response", response.StatusCode.ToString(), response.RequestMessage?.RequestUri?.ToString(),
            response.Headers?.ToString(), response.Content).ConfigureAwait(false);

        return response;
    }

    private async Task LogHttpMessage(string direction, string method, string? uri, string? headers, HttpContent? content)
    {
        var message = new StringBuilder();
        message.AppendLine($"=== HTTP {direction} ===");
        message.AppendLine($"{direction} Line: {method} {uri}");
        
        if (!string.IsNullOrWhiteSpace(headers))
        {
            message.AppendLine($"Headers: {headers}");
        }

        if (content != null)
        {
            try
            {
                var contentString = await content.ReadAsStringAsync().ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(contentString))
                {
                    message.AppendLine($"Content: {contentString}");
                }
            }
            catch (Exception ex)
            {
                message.AppendLine($"Content: [Error reading content: {ex.Message}]");
            }
        }

        message.AppendLine($"=== End HTTP {direction} ===");

        _logger.LogInformation(message.ToString());
    }
}
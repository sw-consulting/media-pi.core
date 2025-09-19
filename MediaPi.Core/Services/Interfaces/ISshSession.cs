// Copyright (c) 2025 sw.consulting
// This file is a part of Media Pi backend

namespace MediaPi.Core.Services.Interfaces
{
    public interface ISshSession : IAsyncDisposable
    {
        Task<string> ExecuteAsync(string command, CancellationToken cancellationToken);
    }
}

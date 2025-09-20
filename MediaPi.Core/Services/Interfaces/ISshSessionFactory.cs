// Copyright (c) 2025 sw.consulting
// This file is a part of Media Pi backend

using MediaPi.Core.Models;

namespace MediaPi.Core.Services.Interfaces
{
    public interface ISshSessionFactory
    {
        Task<ISshSession> CreateAsync(Device device, CancellationToken cancellationToken);
    }

}

using MediaPi.Core.Models;

namespace MediaPi.Core.Services.Interfaces
{
    public interface ISshSessionFactory
    {
        Task<ISshSession> CreateAsync(Device device, CancellationToken cancellationToken);
    }

}

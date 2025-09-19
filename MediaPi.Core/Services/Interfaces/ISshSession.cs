namespace MediaPi.Core.Services.Interfaces
{
    public interface ISshSession : IAsyncDisposable
    {
        Task<string> ExecuteAsync(string command, CancellationToken cancellationToken);
    }
}

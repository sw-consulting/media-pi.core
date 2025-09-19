using MediaPi.Core.Services.Interfaces;
using Renci.SshNet;
using Renci.SshNet.Async;

namespace MediaPi.Core.Services
{
    public sealed class SshNetSession : ISshSession
    {
        private readonly SshClient _client;
        private readonly PrivateKeyFile _privateKeyFile;

        public SshNetSession(SshClient client, PrivateKeyFile privateKeyFile)
        {
            _client = client;
            _privateKeyFile = privateKeyFile;
        }

        public async Task<string> ExecuteAsync(string command, CancellationToken cancellationToken)
        {
            using var sshCommand = _client.CreateCommand(command);
            var factory = new TaskFactory<string>(cancellationToken, TaskCreationOptions.None, TaskContinuationOptions.None, TaskScheduler.Default);
            var output = await sshCommand.ExecuteAsync(factory).ConfigureAwait(false);
            if (string.IsNullOrEmpty(output))
            {
                output = sshCommand.Error;
            }
            if (string.IsNullOrWhiteSpace(output))
            {
                throw new InvalidOperationException($"SSH command '{command}' returned no output.");
            }
            return output;
        }

        public ValueTask DisposeAsync()
        {
            _client.Dispose();
            _privateKeyFile.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}

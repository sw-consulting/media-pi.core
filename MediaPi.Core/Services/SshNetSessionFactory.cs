using MediaPi.Core.Models;
using MediaPi.Core.Services.Interfaces;
using Renci.SshNet;

using SshConnectionInfo = Renci.SshNet.ConnectionInfo;

namespace MediaPi.Core.Services
{
    public sealed class SshNetSessionFactory : ISshSessionFactory
    {
        private readonly ISshClientKeyProvider _keyProvider;
        private readonly ILogger<SshNetSessionFactory> _logger;

        public SshNetSessionFactory(ISshClientKeyProvider keyProvider, ILogger<SshNetSessionFactory> logger)
        {
            _keyProvider = keyProvider ?? throw new ArgumentNullException(nameof(keyProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<ISshSession> CreateAsync(Device device, CancellationToken cancellationToken)
        {
            if (device is null) throw new ArgumentNullException(nameof(device));
            if (string.IsNullOrWhiteSpace(device.IpAddress))
                throw new ArgumentException("Device IP address must be provided.", nameof(device));

            var user = string.IsNullOrWhiteSpace(device.SshUser) ? "pi" : device.SshUser.Trim();
            if (string.IsNullOrWhiteSpace(user))
                throw new InvalidOperationException("SSH user name is missing.");

            var privateKeyPath = _keyProvider.GetPrivateKeyPath();
            if (string.IsNullOrWhiteSpace(privateKeyPath) || !File.Exists(privateKeyPath))
                throw new InvalidOperationException("SSH client private key not found or not configured.");

            PrivateKeyFile privateKeyFile;
            try
            {
                privateKeyFile = new PrivateKeyFile(privateKeyPath);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to load SSH client private key.", ex);
            }

            var authentication = new PrivateKeyAuthenticationMethod(user, privateKeyFile);
            var connectionInfo = new SshConnectionInfo(device.IpAddress, user, authentication);

            var client = new SshClient(connectionInfo);

            // Optional host key pinning using device.PublicKeyOpenSsh (treat it as host public key line if present)
            if (!string.IsNullOrWhiteSpace(device.PublicKeyOpenSsh))
            {
                var parts = device.PublicKeyOpenSsh.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2)
                {
                    var expectedAlgo = parts[0];
                    var expectedBody = parts[1];
                    client.HostKeyReceived += (s, e) =>
                    {
                        try
                        {
                            if (!string.Equals(expectedAlgo, e.HostKeyName, StringComparison.Ordinal))
                            {
                                _logger.LogWarning("Host key algo mismatch for {Ip}: expected {Expected}, got {Actual}", device.IpAddress, expectedAlgo, e.HostKeyName);
                                e.CanTrust = false;
                                return;
                            }
                            var actualBody = Convert.ToBase64String(e.HostKey);
                            if (!string.Equals(actualBody, expectedBody, StringComparison.Ordinal))
                            {
                                _logger.LogWarning("Host key body mismatch for {Ip}", device.IpAddress);
                                e.CanTrust = false;
                                return;
                            }
                            e.CanTrust = true;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error validating host key for {Ip}", device.IpAddress);
                            e.CanTrust = false;
                        }
                    };
                }
            }

            try
            {
                await Task.Run(() => client.Connect(), cancellationToken).ConfigureAwait(false);
                return new SshNetSession(client, privateKeyFile);
            }
            catch
            {
                client.Dispose();
                privateKeyFile.Dispose();
                throw;
            }
        }
    }

}

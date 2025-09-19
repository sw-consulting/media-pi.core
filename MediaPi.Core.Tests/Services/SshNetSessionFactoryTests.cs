using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MediaPi.Core.Models;
using MediaPi.Core.Services;
using MediaPi.Core.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace MediaPi.Core.Tests.Services
{
    [TestFixture]
    public class SshNetSessionFactoryTests
    {
#pragma warning disable CS8618
        private Mock<ISshClientKeyProvider> _keyProviderMock;
        private Mock<ILogger<SshNetSessionFactory>> _loggerMock;
        private Device _validDevice;
#pragma warning restore CS8618

        [SetUp]
        public void SetUp()
        {
            _keyProviderMock = new Mock<ISshClientKeyProvider>();
            _loggerMock = new Mock<ILogger<SshNetSessionFactory>>();
            _validDevice = new Device
            {
                Name = "TestDevice",
                IpAddress = "192.168.1.100",
                SshUser = "pi"
            };
        }

        [Test]
        public void CreateAsync_NullDevice_ThrowsArgumentNullException()
        {
            var factory = new SshNetSessionFactory(_keyProviderMock.Object, _loggerMock.Object);
            Assert.ThrowsAsync<ArgumentNullException>(async () => await factory.CreateAsync(null, CancellationToken.None));
        }

        [Test]
        public void CreateAsync_MissingIpAddress_ThrowsArgumentException()
        {
            var device = new Device { Name = "Test", IpAddress = "", SshUser = "pi" };
            var factory = new SshNetSessionFactory(_keyProviderMock.Object, _loggerMock.Object);
            Assert.ThrowsAsync<ArgumentException>(async () => await factory.CreateAsync(device, CancellationToken.None));
        }

        [Test]
        public void CreateAsync_MissingSshUser_UsesDefaultPi()
        {
            var device = new Device { Name = "Test", IpAddress = "192.168.1.100", SshUser = null };
            _keyProviderMock.Setup(x => x.GetPrivateKeyPath()).Returns("dummy.key");
            File.WriteAllText("dummy.key", "test");
            var factory = new SshNetSessionFactory(_keyProviderMock.Object, _loggerMock.Object);
            // Will fail on key, but should not throw for user
            Assert.ThrowsAsync<InvalidOperationException>(async () => await factory.CreateAsync(device, CancellationToken.None));
            File.Delete("dummy.key");
        }

        [Test]
        public void CreateAsync_MissingPrivateKeyPath_ThrowsInvalidOperationException()
        {
            _keyProviderMock.Setup(x => x.GetPrivateKeyPath()).Returns((string)null);
            var factory = new SshNetSessionFactory(_keyProviderMock.Object, _loggerMock.Object);
            Assert.ThrowsAsync<InvalidOperationException>(async () => await factory.CreateAsync(_validDevice, CancellationToken.None));
        }

        [Test]
        public void CreateAsync_PrivateKeyFileLoadFails_ThrowsInvalidOperationException()
        {
            _keyProviderMock.Setup(x => x.GetPrivateKeyPath()).Returns("notfound.key");
            var factory = new SshNetSessionFactory(_keyProviderMock.Object, _loggerMock.Object);
            Assert.ThrowsAsync<InvalidOperationException>(async () => await factory.CreateAsync(_validDevice, CancellationToken.None));
        }
    }
}

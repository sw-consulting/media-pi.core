// Developed by Maxim [maxirmx] Samsonov (www.sw.consulting)
// This file is a part of Media Pi backend application

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NUnit.Framework;

using MediaPi.Core.Controllers;
using MediaPi.Core.Data;
using MediaPi.Core.RestModels;
using MediaPi.Core;
using System.Threading.Tasks;

namespace MediaPi.Core.Tests.Controllers;

[TestFixture]
public class StatusControllerTests
{
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor.
    private AppDbContext _dbContext;
    private StatusController _controller;
    private ILogger<StatusController> _logger;
#pragma warning restore CS8618

    [SetUp]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: $"status_controller_test_db_{System.Guid.NewGuid()}")
            .Options;
        _dbContext = new AppDbContext(options);
        _logger = new LoggerFactory().CreateLogger<StatusController>();
        _controller = new StatusController(_dbContext, _logger);
    }

    [TearDown]
    public void TearDown()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();
    }

    [Test]
    public async Task Status_ReturnsVersionInformation()
    {
        // Act
        var result = await _controller.Status();

        // Assert
        Assert.That(result.Result, Is.TypeOf<Microsoft.AspNetCore.Mvc.OkObjectResult>());
        var okResult = result.Result as Microsoft.AspNetCore.Mvc.OkObjectResult;
        Assert.That(okResult, Is.Not.Null);

        var status = okResult!.Value as Status;
        Assert.That(status, Is.Not.Null);

        Assert.That(status!.Msg, Does.Contain("MediaPi Core"));
        Assert.That(status.AppVersion, Is.EqualTo(VersionInfo.AppVersion));
        Assert.That(status.DbVersion, Is.Not.Null.And.Not.Empty);
    }
}

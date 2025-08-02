// MIT License
//
// Copyright (c) 2025 Maxim [maxirmx] Samsonov (www.sw.consulting)
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

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

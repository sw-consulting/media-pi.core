// Copyright (C) 2025 Maxim [maxirmx] Samsonov (www.sw.consulting)
// All rights reserved.
// This file is a part of Fuelflux core application
//
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions
// are met:
// 1. Redistributions of source code must retain the above copyright
// notice, this list of conditions and the following disclaimer.
// 2. Redistributions in binary form must reproduce the above copyright
// notice, this list of conditions and the following disclaimer in the
// documentation and/or other materials provided with the distribution.
//
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
// ``AS IS'' AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED
// TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR
// PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDERS OR CONTRIBUTORS
// BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR
// CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF
// SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS
// INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN
// CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE)
// ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE
// POSSIBILITY OF SUCH DAMAGE.

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NUnit.Framework;

using Fuelflux.Core.Controllers;
using Fuelflux.Core.Data;
using Fuelflux.Core.RestModels;
using Fuelflux.Core;
using System.Threading.Tasks;

namespace Fuelflux.Core.Tests.Controllers;

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

        Assert.That(status!.Msg, Does.Contain("Fuelflux Core"));
        Assert.That(status.AppVersion, Is.EqualTo(VersionInfo.AppVersion));
        Assert.That(status.DbVersion, Is.Not.Null.And.Not.Empty);
    }
}

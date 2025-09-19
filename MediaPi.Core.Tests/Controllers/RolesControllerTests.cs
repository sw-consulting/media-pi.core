// Copyright (c) 2025 sw.consulting
// This file is a part of Media Pi backend

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using System.Linq;
using System.Threading.Tasks;

using MediaPi.Core.Controllers;
using MediaPi.Core.Data;
using MediaPi.Core.Models;

namespace MediaPi.Core.Tests.Controllers
{
    [TestFixture]
    public class RolesControllerTests
    {
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
        private Mock<IHttpContextAccessor> _mockHttpContextAccessor;
        private Mock<ILogger<RolesController>> _mockLogger;
        private AppDbContext _dbContext;
        private RolesController _controller;
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

        [SetUp]
        public void Setup()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase($"roles_controller_test_db_{System.Guid.NewGuid()}")
                .Options;

            _dbContext = new AppDbContext(options);

            // Seed roles
            var adminRole = new Role { Id = 1, RoleId = UserRoleConstants.SystemAdministrator, Name = "Администратор системы" };
            var managerRole = new Role { Id = 2, RoleId = UserRoleConstants.AccountManager, Name = "Менеджер лицевого счёта" };
            var engineerRole = new Role { Id = 3, RoleId = UserRoleConstants.InstallationEngineer, Name = "Инженер-установщик" };
            
            _dbContext.Roles.AddRange(adminRole, managerRole, engineerRole);
            _dbContext.SaveChanges();

            _mockHttpContextAccessor = new Mock<IHttpContextAccessor>();
            _mockLogger = new Mock<ILogger<RolesController>>();

            // Setup authenticated user
            var context = new DefaultHttpContext();
            context.Items["UserId"] = 1; // Any user ID to simulate authenticated user
            _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns(context);

            _controller = new RolesController(
                _mockHttpContextAccessor.Object,
                _dbContext,
                _mockLogger.Object
            )
            {
                ControllerContext = new ControllerContext { HttpContext = context }
            };
        }

        [TearDown]
        public void TearDown()
        {
            _dbContext.Database.EnsureDeleted();
            _dbContext.Dispose();
        }

        [Test]
        public async Task GetAll_ReturnsAllRoles()
        {
            // Act
            var result = await _controller.GetAll();

            // Assert
            Assert.That(result?.Value, Is.Not.Null);
            var roles = result?.Value?.ToList();
            Assert.That(roles?.Count, Is.EqualTo(3));
            if (roles == null || roles.Count < 3) return;
            Assert.That(roles[0].RoleId, Is.EqualTo(UserRoleConstants.SystemAdministrator));
            Assert.That(roles[1].RoleId, Is.EqualTo(UserRoleConstants.AccountManager));
            Assert.That(roles[2].RoleId, Is.EqualTo(UserRoleConstants.InstallationEngineer));
        }

        [Test]
        public async Task GetById_ExistingId_ReturnsRole()
        {
            // Act
            var result = await _controller.GetById(1);

            // Assert
            Assert.That(result?.Value, Is.Not.Null);
            Assert.That(result?.Value?.Id, Is.EqualTo(1));
            Assert.That(result?.Value?.RoleId, Is.EqualTo(UserRoleConstants.SystemAdministrator));
            Assert.That(result?.Value?.Name, Is.EqualTo("Администратор системы"));
        }

        [Test]
        public async Task GetById_NonExistingId_Returns404()
        {
            // Act
            var result = await _controller.GetById(999);

            // Assert
            Assert.That(result.Result, Is.TypeOf<ObjectResult>());
            var objResult = result.Result as ObjectResult;
            Assert.That(objResult!.StatusCode, Is.EqualTo(StatusCodes.Status404NotFound));
        }

        [Test]
        public async Task GetByRoleId_ExistingRoleId_ReturnsRole()
        {
            // Act
            var result = await _controller.GetByRoleId(UserRoleConstants.AccountManager);

            // Assert
            Assert.That(result.Value, Is.Not.Null);
            Assert.That(result?.Value?.RoleId, Is.EqualTo(UserRoleConstants.AccountManager));
            Assert.That(result?.Value?.Name, Is.EqualTo("Менеджер лицевого счёта"));
        }

        [Test]
        public async Task GetByRoleId_NonExistingRoleId_Returns404()
        {
            // Act - cast to UserRoleConstants since we need to pass a value that's not in the enum
            var result = await _controller.GetByRoleId((UserRoleConstants)999);

            // Assert
            Assert.That(result.Result, Is.TypeOf<ObjectResult>());
            var objResult = result.Result as ObjectResult;
            Assert.That(objResult!.StatusCode, Is.EqualTo(StatusCodes.Status404NotFound));
        }
    }
}

// Developed by Maxim [maxirmx] Samsonov (www.sw.consulting)
// This file is a part of Media Pi backend application

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
using MediaPi.Core.RestModels;

namespace MediaPi.Core.Tests.Controllers
{
    [TestFixture]
    public class RolesControllerAuthorizationTests
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
                .UseInMemoryDatabase($"roles_controller_auth_test_db_{System.Guid.NewGuid()}")
                .Options;

            _dbContext = new AppDbContext(options);
            
            // Seed roles
            var adminRole = new Role { Id = 1, RoleId = UserRoleConstants.SystemAdministrator, Name = "Администратор системы" };
            var managerRole = new Role { Id = 2, RoleId = UserRoleConstants.AccountManager, Name = "Менеджер лицевого счёта" };
            var engineerRole = new Role { Id = 3, RoleId = UserRoleConstants.InstallationEngineer, Name = "Инженер-установщик" };
            
            _dbContext.Roles.AddRange(adminRole, managerRole, engineerRole);
            
            // Create users with different roles
            var adminUser = new User
            {
                Id = 1,
                Email = "admin@example.com",
                Password = "password",
                FirstName = "Admin",
                LastName = "User",
                Patronymic = "",
                UserRoles = [new UserRole { UserId = 1, RoleId = adminRole.Id, Role = adminRole }]
            };
            
            var managerUser = new User
            {
                Id = 2,
                Email = "manager@example.com",
                Password = "password",
                FirstName = "Manager",
                LastName = "User",
                Patronymic = "",
                UserRoles = [new UserRole { UserId = 2, RoleId = managerRole.Id, Role = managerRole }]
            };
            
            var engineerUser = new User
            {
                Id = 3,
                Email = "engineer@example.com",
                Password = "password",
                FirstName = "Engineer",
                LastName = "User",
                Patronymic = "",
                UserRoles = [new UserRole { UserId = 3, RoleId = engineerRole.Id, Role = engineerRole }]
            };
            
            _dbContext.Users.AddRange(adminUser, managerUser, engineerUser);
            _dbContext.SaveChanges();

            _mockHttpContextAccessor = new Mock<IHttpContextAccessor>();
            _mockLogger = new Mock<ILogger<RolesController>>();
        }

        private void SetupController(int? userId)
        {
            var context = new DefaultHttpContext();
            if (userId.HasValue)
            {
                context.Items["UserId"] = userId.Value;
            }
            
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
        public async Task GetAll_AdminUser_ReturnsAllRoles()
        {
            // Arrange
            SetupController(1); // Admin user

            // Act
            var result = await _controller.GetAll();

            // Assert
            Assert.That(result?.Value, Is.Not.Null);
            var roles = result?.Value?.ToList();
            Assert.That(roles?.Count, Is.EqualTo(3));
        }



        [Test]
        public async Task GetById_ManagerUser_ReturnsRole()
        {
            // Arrange
            SetupController(2); // Manager user

            // Act
            var result = await _controller.GetById(2);

            // Assert
            Assert.That(result?.Value, Is.Not.Null);
            Assert.That(result?.Value?.Id, Is.EqualTo(2));
        }


        [Test]
        public async Task GetByRoleId_AdminUser_ReturnsRole()
        {
            // Arrange
            SetupController(1); // Admin user

            // Act
            var result = await _controller.GetByRoleId(UserRoleConstants.SystemAdministrator);

            // Assert
            Assert.That(result?.Value, Is.Not.Null);
            Assert.That(result?.Value?.RoleId, Is.EqualTo(UserRoleConstants.SystemAdministrator));
        }

    }
}
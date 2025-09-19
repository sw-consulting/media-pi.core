// Copyright (c) 2025 sw.consulting
// This file is a part of Media Pi backend

using Microsoft.EntityFrameworkCore;
using MediaPi.Core.Models;

namespace MediaPi.Core.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options) { }

        public DbSet<User> Users => Set<User>();
        public DbSet<Role> Roles => Set<Role>();
        public DbSet<UserRole> UserRoles => Set<UserRole>();
        public DbSet<Video> Videos => Set<Video>();
        public DbSet<Playlist> Playlists => Set<Playlist>();
        public DbSet<VideoPlaylist> VideoPlaylists => Set<VideoPlaylist>();
        public DbSet<Screenshot> Screenshots => Set<Screenshot>();
        public DbSet<Device> Devices => Set<Device>();
        public DbSet<DeviceGroup> DeviceGroups => Set<DeviceGroup>();
        public DbSet<Category> Categories => Set<Category>();
        public DbSet<Account> Accounts => Set<Account>();
        public DbSet<Subscription> Subscriptions => Set<Subscription>();
        public DbSet<VideoStatus> VideoStatuses => Set<VideoStatus>();
        public DbSet<UserAccount> UserAccounts => Set<UserAccount>();
        public DbSet<DeviceProbe> DeviceProbes => Set<DeviceProbe>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<UserRole>()
                .HasKey(ur => new { ur.UserId, ur.RoleId });

            modelBuilder.Entity<UserRole>()
                .HasOne(ur => ur.User)
                .WithMany(u => u.UserRoles)
                .HasForeignKey(ur => ur.UserId);

            modelBuilder.Entity<UserRole>()
                .HasOne(ur => ur.Role)
                .WithMany(r => r.UserRoles)
                .HasForeignKey(ur => ur.RoleId);

            modelBuilder.Entity<UserAccount>()
                .HasKey(ua => new { ua.UserId, ua.AccountId });

            modelBuilder.Entity<UserAccount>()
                .HasOne(ua => ua.User)
                .WithMany(u => u.UserAccounts)
                .HasForeignKey(ua => ua.UserId);

            modelBuilder.Entity<UserAccount>()
                .HasOne(ua => ua.Account)
                .WithMany(a => a.UserAccounts)
                .HasForeignKey(ua => ua.AccountId);

            modelBuilder.Entity<VideoPlaylist>()
                .HasKey(vp => new { vp.VideoId, vp.PlaylistId });

            modelBuilder.Entity<VideoPlaylist>()
                .HasOne(vp => vp.Video)
                .WithMany(v => v.VideoPlaylists)
                .HasForeignKey(vp => vp.VideoId);

            modelBuilder.Entity<VideoPlaylist>()
                .HasOne(vp => vp.Playlist)
                .WithMany(p => p.VideoPlaylists)
                .HasForeignKey(vp => vp.PlaylistId);

            modelBuilder.Entity<Device>()
                .HasOne(d => d.Account)
                .WithMany(a => a.Devices)
                .HasForeignKey(d => d.AccountId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<Device>()
                .HasOne(d => d.DeviceGroup)
                .WithMany(g => g.Devices)
                .HasForeignKey(d => d.DeviceGroupId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<VideoStatus>().HasData(
                new VideoStatus { Id = StatusConstants.Queued, Name = "Queued" },
                new VideoStatus { Id = StatusConstants.Loading, Name = "Loading" },
                new VideoStatus { Id = StatusConstants.Loaded, Name = "Loaded" },
                new VideoStatus { Id = StatusConstants.Playing, Name = "Playing" }
            );

            modelBuilder.Entity<Role>().HasData(
                new Role { Id = 1, RoleId = UserRoleConstants.SystemAdministrator, Name = "Администратор системы" },
                new Role { Id = 2, RoleId = UserRoleConstants.AccountManager, Name = "Менеджер лицевого счёта" },
                new Role { Id = 3, RoleId = UserRoleConstants.InstallationEngineer, Name = "Инженер-установщик" }
            );

            modelBuilder.Entity<User>().HasData(
                new User
                {
                    Id = 1,
                    FirstName = "Maxim",
                    LastName = "Samsonov",
                    Patronymic = "",
                    Email = "maxirmx@sw.consulting",
                    Password = "$2b$12$eOXzlwFzyGVERe0sNwFeJO5XnvwsjloUpL4o2AIQ8254RT88MnsDi"
                }
            );

            modelBuilder.Entity<UserRole>().HasData(
                new UserRole { UserId = 1, RoleId = 1 } // Admininstrator
            );
        }
    }
}

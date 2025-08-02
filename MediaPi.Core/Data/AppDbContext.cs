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
        public DbSet<VideoAtDevice> VideoAtDevices => Set<VideoAtDevice>();
        public DbSet<PlaylistAtDeviceGroup> PlaylistAtDeviceGroups => Set<PlaylistAtDeviceGroup>();
        public DbSet<Category> Categories => Set<Category>();
        public DbSet<Account> Accounts => Set<Account>();
        public DbSet<Subscription> Subscriptions => Set<Subscription>();
        public DbSet<VideoStatus> VideoStatuses => Set<VideoStatus>();
        public DbSet<UserAccount> UserAccounts => Set<UserAccount>();

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

            modelBuilder.Entity<VideoAtDevice>()
                .HasKey(vd => new { vd.DeviceId, vd.VideoId });

            modelBuilder.Entity<VideoAtDevice>()
                .HasOne(vd => vd.Device)
                .WithMany(d => d.VideoAtDevices)
                .HasForeignKey(vd => vd.DeviceId);

            modelBuilder.Entity<VideoAtDevice>()
                .HasOne(vd => vd.Video)
                .WithMany(v => v.VideoAtDevices)
                .HasForeignKey(vd => vd.VideoId);

            modelBuilder.Entity<VideoAtDevice>()
                .HasOne(vd => vd.Status)
                .WithMany(s => s.VideoAtDevices)
                .HasForeignKey(vd => vd.StatusId);

            modelBuilder.Entity<PlaylistAtDeviceGroup>()
                .HasKey(pd => new { pd.DeviceGroupId, pd.PlaylistId });

            modelBuilder.Entity<PlaylistAtDeviceGroup>()
                .HasOne(pd => pd.DeviceGroup)
                .WithMany(dg => dg.PlaylistAtDeviceGroups)
                .HasForeignKey(pd => pd.DeviceGroupId);

            modelBuilder.Entity<PlaylistAtDeviceGroup>()
                .HasOne(pd => pd.Playlist)
                .WithMany(p => p.PlaylistAtDeviceGroups)
                .HasForeignKey(pd => pd.PlaylistId);

            modelBuilder.Entity<PlaylistAtDeviceGroup>()
                .HasOne(pd => pd.Status)
                .WithMany(s => s.PlaylistAtDeviceGroups)
                .HasForeignKey(pd => pd.StatusId);

            modelBuilder.Entity<Device>()
                .HasOne(d => d.DeviceGroup)
                .WithMany(g => g.Devices)
                .HasForeignKey(d => d.DeviceGroupId);

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

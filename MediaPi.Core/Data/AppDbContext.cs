// Copyright (C) 2025-2026 sw.consulting
// This file is a part of Media Pi backend

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
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
        public DbSet<PlaylistDeviceGroup> PlaylistDeviceGroups => Set<PlaylistDeviceGroup>();
        public DbSet<UserAccount> UserAccounts => Set<UserAccount>();
        public DbSet<DeviceProbe> DeviceProbes => Set<DeviceProbe>();

        public override int SaveChanges() => SaveChanges(true);

        public override int SaveChanges(bool acceptAllChangesOnSuccess)
        {
            ApplyPlaylistTimestamps();
            return base.SaveChanges(acceptAllChangesOnSuccess);
        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
            SaveChangesAsync(true, cancellationToken);

        public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
        {
            await ApplyPlaylistTimestampsAsync(cancellationToken);
            return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }

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
                .HasKey(vp => vp.Id);

            modelBuilder.Entity<VideoPlaylist>()
                .HasOne(vp => vp.Video)
                .WithMany(v => v.VideoPlaylists)
                .HasForeignKey(vp => vp.VideoId);

            modelBuilder.Entity<VideoPlaylist>()
                .HasOne(vp => vp.Playlist)
                .WithMany(p => p.VideosPlaylist)
                .HasForeignKey(vp => vp.PlaylistId);

            modelBuilder.Entity<PlaylistDeviceGroup>()
                .HasOne(vp => vp.Playlist)
                .WithMany(p => p.PlaylistDeviceGroups)
                .HasForeignKey(vp => vp.PlaylistId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<PlaylistDeviceGroup>()
                .HasOne(vp => vp.DeviceGroup)
                .WithMany(p => p.PlaylistsDeviceGroup)
                .HasForeignKey(vp => vp.DeviceGroupId)
                .OnDelete(DeleteBehavior.Cascade);

            // Configure Video entity - explicitly map uint properties to bigint
            // This documents the intentional type mismatch for clarity
            modelBuilder.Entity<Video>()
                .Property(v => v.FileSizeBytes)
                .HasColumnType("bigint")
                .HasComment("Stores uint values (0 to 4,294,967,295) in bigint column for EF Core compatibility");

            modelBuilder.Entity<Video>()
                .Property(v => v.DurationSeconds)
                .HasColumnType("bigint")
                .HasComment("Stores uint values (0 to 4,294,967,295) in bigint column for EF Core compatibility");

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

            modelBuilder.Entity<PlaylistDeviceGroup>()
                 .HasIndex(pdg => pdg.DeviceGroupId)
                 .IsUnique()
                 .HasFilter("\"play\" = true");

            modelBuilder.Entity<Playlist>()
                .Property(p => p.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            modelBuilder.Entity<Playlist>()
                .Property(p => p.UpdatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            modelBuilder.Entity<Category>()
                .Property(c => c.Free)
                .HasDefaultValue(true)
                .ValueGeneratedNever();

            modelBuilder.Entity<Category>()
                .HasIndex(c => c.Title)
                .IsUnique()
                .HasDatabaseName("IX_categories_title");

            modelBuilder.Entity<Video>()
                .HasOne(v => v.Category)
                .WithMany(c => c.Videos)
                .HasForeignKey(v => v.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Subscription>()
                .HasOne(s => s.Category)
                .WithMany(c => c.Subscriptions)
                .HasForeignKey(s => s.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);
    
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
                new UserRole { UserId = 1, RoleId = 1 }
            );
        }

        private void ApplyPlaylistTimestamps()
        {
            var (updatedAt, playlistEntries, touchedPlaylistIds) = PreparePlaylistTimestampChanges();
            TouchPlaylists(touchedPlaylistIds, updatedAt, playlistEntries);
        }

        private async Task ApplyPlaylistTimestampsAsync(CancellationToken cancellationToken)
        {
            var (updatedAt, playlistEntries, touchedPlaylistIds) = PreparePlaylistTimestampChanges();
            await TouchPlaylistsAsync(touchedPlaylistIds, updatedAt, playlistEntries, cancellationToken);
        }

        private (DateTime UpdatedAt, List<EntityEntry<Playlist>> PlaylistEntries, List<int> TouchedPlaylistIds) PreparePlaylistTimestampChanges()
        {
            ChangeTracker.DetectChanges();

            var now = DateTime.UtcNow;
            var playlistEntries = ChangeTracker.Entries<Playlist>().ToList();

            foreach (var entry in playlistEntries)
            {
                if (entry.State == EntityState.Added)
                {
                    if (entry.Entity.CreatedAt == default)
                    {
                        entry.Entity.CreatedAt = now;
                    }

                    if (entry.Entity.UpdatedAt == default)
                    {
                        entry.Entity.UpdatedAt = entry.Entity.CreatedAt;
                    }
                }
                else if (entry.State == EntityState.Modified && HasPlaylistContentChange(entry))
                {
                    entry.Entity.UpdatedAt = now;
                }
            }

            var deletedPlaylistIds = playlistEntries
                .Where(entry => entry.State == EntityState.Deleted)
                .Select(entry => entry.Entity.Id)
                .ToHashSet();
            var touchedPlaylistIds = ChangeTracker.Entries<VideoPlaylist>()
                .Where(entry => entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
                .SelectMany(GetAffectedPlaylistIds)
                .Where(id => id > 0 && !deletedPlaylistIds.Contains(id))
                .Distinct()
                .ToList();

            return (now, playlistEntries, touchedPlaylistIds);
        }

        private static bool HasPlaylistContentChange(EntityEntry<Playlist> entry) =>
            entry.Properties.Any(property =>
                property.IsModified
                && property.Metadata.Name is not nameof(Playlist.CreatedAt)
                && property.Metadata.Name is not nameof(Playlist.UpdatedAt));

        private static IEnumerable<int> GetAffectedPlaylistIds(EntityEntry<VideoPlaylist> entry)
        {
            if (entry.State is EntityState.Modified or EntityState.Deleted)
            {
                var originalPlaylistId = entry.Property(vp => vp.PlaylistId).OriginalValue;
                if (originalPlaylistId > 0)
                {
                    yield return originalPlaylistId;
                }
            }

            if (entry.State is EntityState.Added or EntityState.Modified)
            {
                var currentPlaylistId = entry.Entity.PlaylistId;
                if (currentPlaylistId > 0)
                {
                    yield return currentPlaylistId;
                }
            }
        }

        private void TouchPlaylists(
            IReadOnlyCollection<int> playlistIds,
            DateTime updatedAt,
            IReadOnlyCollection<EntityEntry<Playlist>> playlistEntries)
        {
            var untrackedIds = TouchTrackedPlaylists(playlistIds, updatedAt, playlistEntries);
            if (untrackedIds.Count == 0) return;

            var playlists = Playlists
                .Where(playlist => untrackedIds.Contains(playlist.Id))
                .ToList();

            foreach (var playlist in playlists)
            {
                playlist.UpdatedAt = updatedAt;
            }
        }

        private async Task TouchPlaylistsAsync(
            IReadOnlyCollection<int> playlistIds,
            DateTime updatedAt,
            IReadOnlyCollection<EntityEntry<Playlist>> playlistEntries,
            CancellationToken cancellationToken)
        {
            var untrackedIds = TouchTrackedPlaylists(playlistIds, updatedAt, playlistEntries);
            if (untrackedIds.Count == 0) return;

            var playlists = await Playlists
                .Where(playlist => untrackedIds.Contains(playlist.Id))
                .ToListAsync(cancellationToken);

            foreach (var playlist in playlists)
            {
                playlist.UpdatedAt = updatedAt;
            }
        }

        private static List<int> TouchTrackedPlaylists(
            IReadOnlyCollection<int> playlistIds,
            DateTime updatedAt,
            IReadOnlyCollection<EntityEntry<Playlist>> playlistEntries)
        {
            var untrackedIds = new List<int>();
            foreach (var playlistId in playlistIds)
            {
                var trackedEntry = playlistEntries.FirstOrDefault(entry => entry.Entity.Id == playlistId);
                if (trackedEntry == null)
                {
                    untrackedIds.Add(playlistId);
                    continue;
                }

                if (trackedEntry.State is EntityState.Added or EntityState.Deleted)
                {
                    continue;
                }

                trackedEntry.Entity.UpdatedAt = updatedAt;
            }

            return untrackedIds;
        }
    }
}

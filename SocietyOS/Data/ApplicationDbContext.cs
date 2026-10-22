using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SocietyOS.Models;

namespace SocietyOS.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Building> Buildings => Set<Building>();
        public DbSet<Flat> Flats => Set<Flat>();
        public DbSet<ResidentProfile> ResidentProfiles => Set<ResidentProfile>();
        public DbSet<SecurityGuard> SecurityGuards => Set<SecurityGuard>();
        public DbSet<VisitorRequest> VisitorRequests => Set<VisitorRequest>();

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Building & Flat topology
            builder.Entity<Flat>(entity =>
            {
                entity.HasIndex(f => new { f.BuildingId, f.FlatNumber }).IsUnique();
                entity.HasOne(f => f.Building)
                      .WithMany(b => b.Flats)
                      .HasForeignKey(f => f.BuildingId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // Resident Profile relationships
            builder.Entity<ResidentProfile>(entity =>
            {
                entity.HasOne(r => r.User)
                      .WithOne(u => u.ResidentProfile)
                      .HasForeignKey<ResidentProfile>(r => r.UserId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(r => r.Flat)
                      .WithMany(f => f.Residents)
                      .HasForeignKey(r => r.FlatId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // Security Guard relationships
            builder.Entity<SecurityGuard>(entity =>
            {
                entity.HasIndex(g => g.EmployeeCode).IsUnique();

                entity.HasOne(g => g.User)
                      .WithOne(u => u.SecurityGuardProfile)
                      .HasForeignKey<SecurityGuard>(g => g.UserId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<VisitorRequest>(entity =>
            {
                // Indexes to optimize guard queries, resident pending lookups, and the retention cleanup worker
                entity.HasIndex(v => v.FlatId);
                entity.HasIndex(v => v.Status);
                entity.HasIndex(v => v.ExpiresAtUtc);
                entity.HasIndex(v => new { v.FlatId, v.Status });

                // Restrict deletion so deleting a visitor request never deletes flats or user accounts
                entity.HasOne(v => v.Flat)
                      .WithMany()
                      .HasForeignKey(v => v.FlatId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(v => v.RegisteredByGuard)
                      .WithMany()
                      .HasForeignKey(v => v.RegisteredByGuardId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(v => v.DecisionByResident)
                      .WithMany()
                      .HasForeignKey(v => v.DecisionByResidentId)
                      .OnDelete(DeleteBehavior.Restrict);
            });
        }
    }
}
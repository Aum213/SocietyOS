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
        public DbSet<CommunityIssue> CommunityIssues => Set<CommunityIssue>();
        public DbSet<IssueVote> IssueVotes => Set<IssueVote>();
        public DbSet<IssueComment> IssueComments => Set<IssueComment>();
        public DbSet<IssueStatusHistory> IssueStatusHistories => Set<IssueStatusHistory>();
        public DbSet<Announcement> Announcements => Set<Announcement>();
        public DbSet<AnnouncementRead> AnnouncementReads => Set<AnnouncementRead>();

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
            // CommunityIssue relationships and feed indexes
            builder.Entity<CommunityIssue>(entity =>
            {
                entity.HasIndex(i => i.Status);
                entity.HasIndex(i => i.Category);
                entity.HasIndex(i => i.CreatedAtUtc);
                entity.HasIndex(i => new { i.Status, i.Category });

                entity.HasOne(i => i.ReporterUser)
                      .WithMany()
                      .HasForeignKey(i => i.ReporterUserId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(i => i.Building)
                      .WithMany()
                      .HasForeignKey(i => i.BuildingId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(i => i.Flat)
                      .WithMany()
                      .HasForeignKey(i => i.FlatId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // IssueVote composite uniqueness rule: 1 vote per resident per issue
            builder.Entity<IssueVote>(entity =>
            {
                entity.HasIndex(v => new { v.IssueId, v.VoterUserId }).IsUnique();

                entity.HasOne(v => v.Issue)
                      .WithMany(i => i.Votes)
                      .HasForeignKey(v => v.IssueId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(v => v.VoterUser)
                      .WithMany()
                      .HasForeignKey(v => v.VoterUserId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // IssueComment relationships
            builder.Entity<IssueComment>(entity =>
            {
                entity.HasIndex(c => c.IssueId);

                entity.HasOne(c => c.Issue)
                      .WithMany(i => i.Comments)
                      .HasForeignKey(c => c.IssueId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(c => c.AuthorUser)
                      .WithMany()
                      .HasForeignKey(c => c.AuthorUserId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // IssueStatusHistory audit relationships
            builder.Entity<IssueStatusHistory>(entity =>
            {
                entity.HasIndex(h => h.IssueId);

                entity.HasOne(h => h.Issue)
                      .WithMany(i => i.StatusHistory)
                      .HasForeignKey(h => h.IssueId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(h => h.ChangedByAdmin)
                      .WithMany()
                      .HasForeignKey(h => h.ChangedByAdminId)
                      .OnDelete(DeleteBehavior.Restrict);
            });
            // Announcement indexes and relationships
            builder.Entity<Announcement>(entity =>
            {
                entity.HasIndex(a => a.State);
                entity.HasIndex(a => a.PublishedAtUtc);
                entity.HasIndex(a => a.ExpiresAtUtc);
                entity.HasIndex(a => a.Category);
                entity.HasIndex(a => a.Priority);
                entity.HasIndex(a => new { a.State, a.IsPinned, a.Priority });

                entity.HasOne(a => a.CreatedByAdmin)
                      .WithMany()
                      .HasForeignKey(a => a.CreatedByAdminId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(a => a.LastEditedByAdmin)
                      .WithMany()
                      .HasForeignKey(a => a.LastEditedByAdminId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // Per-user AnnouncementRead composite uniqueness constraint
            builder.Entity<AnnouncementRead>(entity =>
            {
                entity.HasIndex(r => new { r.AnnouncementId, r.UserId }).IsUnique();

                entity.HasOne(r => r.Announcement)
                      .WithMany(a => a.Reads)
                      .HasForeignKey(r => r.AnnouncementId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(r => r.User)
                      .WithMany()
                      .HasForeignKey(r => r.UserId)
                      .OnDelete(DeleteBehavior.Restrict);
            });
        }
    }
}
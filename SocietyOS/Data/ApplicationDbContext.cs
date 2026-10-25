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

        //Amenities booking phase
        public DbSet<Amenity> Amenities => Set<Amenity>();
        public DbSet<AmenityBooking> AmenityBookings => Set<AmenityBooking>();
        public DbSet<AmenityClosure> AmenityClosures => Set<AmenityClosure>();

        //Daily Help
        public DbSet<DailyHelpListing> DailyHelpListings => Set<DailyHelpListing>();
        public DbSet<DailyHelpShortlist> DailyHelpShortlists => Set<DailyHelpShortlist>();

        //EMERGENCY SOS 
        public DbSet<EmergencyIncident> EmergencyIncidents => Set<EmergencyIncident>();
        public DbSet<EmergencyStatusHistory> EmergencyStatusHistories => Set<EmergencyStatusHistory>();
        public DbSet<SocietyEmergencyContact> SocietyEmergencyContacts => Set<SocietyEmergencyContact>();

        //Marketplace Listings
        public DbSet<MarketplaceListing> MarketplaceListings => Set<MarketplaceListing>();
        public DbSet<MarketplaceListingReport> MarketplaceListingReports => Set<MarketplaceListingReport>();

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
            // Amenity configuration
            builder.Entity<Amenity>(entity =>
            {
                entity.HasIndex(a => a.IsActive);
                entity.HasIndex(a => a.Name);
            });

            // AmenityBooking relationships and interval query indexes
            builder.Entity<AmenityBooking>(entity =>
            {
                entity.HasIndex(b => new { b.AmenityId, b.Status, b.StartTimeUtc, b.EndTimeUtc });
                entity.HasIndex(b => new { b.ResidentUserId, b.Status, b.StartTimeUtc });
                entity.HasIndex(b => b.FlatId);

                // Restrict deletion so historical bookings are never destroyed if an amenity or user is modified
                entity.HasOne(b => b.Amenity)
                      .WithMany(a => a.Bookings)
                      .HasForeignKey(b => b.AmenityId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(b => b.ResidentUser)
                      .WithMany()
                      .HasForeignKey(b => b.ResidentUserId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(b => b.Flat)
                      .WithMany()
                      .HasForeignKey(b => b.FlatId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(b => b.CancelledByUser)
                      .WithMany()
                      .HasForeignKey(b => b.CancelledByUserId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // AmenityClosure relationships and interval index
            builder.Entity<AmenityClosure>(entity =>
            {
                entity.HasIndex(c => new { c.AmenityId, c.StartTimeUtc, c.EndTimeUtc });

                entity.HasOne(c => c.Amenity)
                      .WithMany(a => a.Closures)
                      .HasForeignKey(c => c.AmenityId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(c => c.CreatedByAdmin)
                      .WithMany()
                      .HasForeignKey(c => c.CreatedByAdminId)
                      .OnDelete(DeleteBehavior.Restrict);
            });
            // DailyHelpListing indexes and non-cascading audit foreign keys
            builder.Entity<DailyHelpListing>(entity =>
            {
                entity.HasIndex(d => d.Status);
                entity.HasIndex(d => d.Category);
                entity.HasIndex(d => new { d.Status, d.Category });

                entity.HasOne(d => d.VerifiedByAdmin)
                      .WithMany()
                      .HasForeignKey(d => d.VerifiedByAdminId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(d => d.SubmittedByResidentUser)
                      .WithMany()
                      .HasForeignKey(d => d.SubmittedByResidentUserId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(d => d.LastModifiedByAdmin)
                      .WithMany()
                      .HasForeignKey(d => d.LastModifiedByAdminId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // DailyHelpShortlist composite uniqueness constraint: 1 shortlist record per user per listing
            builder.Entity<DailyHelpShortlist>(entity =>
            {
                entity.HasIndex(s => new { s.ListingId, s.UserId }).IsUnique();

                entity.HasOne(s => s.Listing)
                      .WithMany(d => d.Shortlists)
                      .HasForeignKey(s => s.ListingId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(s => s.User)
                      .WithMany()
                      .HasForeignKey(s => s.UserId)
                      .OnDelete(DeleteBehavior.Restrict);
            });
            // EmergencyIncident indexes and non-cascading foreign keys
            builder.Entity<EmergencyIncident>(entity =>
            {
                entity.HasIndex(e => e.ReferenceCode).IsUnique();
                entity.HasIndex(e => new { e.Status, e.CreatedAtUtc });
                entity.HasIndex(e => new { e.ReporterUserId, e.CreatedAtUtc });
                entity.HasIndex(e => e.FlatId);

                entity.HasOne(e => e.ReporterUser)
                      .WithMany()
                      .HasForeignKey(e => e.ReporterUserId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.Flat)
                      .WithMany()
                      .HasForeignKey(e => e.FlatId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.Building)
                      .WithMany()
                      .HasForeignKey(e => e.BuildingId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.AcknowledgedByUser)
                      .WithMany()
                      .HasForeignKey(e => e.AcknowledgedByUserId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.ResponderUser)
                      .WithMany()
                      .HasForeignKey(e => e.ResponderUserId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.ResolvedByUser)
                      .WithMany()
                      .HasForeignKey(e => e.ResolvedByUserId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.CancelledByUser)
                      .WithMany()
                      .HasForeignKey(e => e.CancelledByUserId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // EmergencyStatusHistory audit log
            builder.Entity<EmergencyStatusHistory>(entity =>
            {
                entity.HasIndex(h => h.IncidentId);

                entity.HasOne(h => h.Incident)
                      .WithMany(e => e.StatusHistory)
                      .HasForeignKey(h => h.IncidentId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(h => h.ActorUser)
                      .WithMany()
                      .HasForeignKey(h => h.ActorUserId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // SocietyEmergencyContact configuration
            builder.Entity<SocietyEmergencyContact>(entity =>
            {
                entity.HasIndex(c => new { c.IsActive, c.DisplayOrder });

                entity.HasOne(c => c.UpdatedByAdmin)
                      .WithMany()
                      .HasForeignKey(c => c.UpdatedByAdminId)
                      .OnDelete(DeleteBehavior.Restrict);
            });
            // MarketplaceListing indexes and non-cascading foreign keys
            builder.Entity<MarketplaceListing>(entity =>
            {
                entity.HasIndex(m => new { m.Status, m.Category, m.CreatedAtUtc });
                entity.HasIndex(m => new { m.Status, m.ListingType });
                entity.HasIndex(m => new { m.SellerUserId, m.CreatedAtUtc });

                entity.HasOne(m => m.SellerUser)
                      .WithMany()
                      .HasForeignKey(m => m.SellerUserId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(m => m.Flat)
                      .WithMany()
                      .HasForeignKey(m => m.FlatId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(m => m.Building)
                      .WithMany()
                      .HasForeignKey(m => m.BuildingId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(m => m.ModeratedByAdmin)
                      .WithMany()
                      .HasForeignKey(m => m.ModeratedByAdminId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // MarketplaceListingReport composite uniqueness constraint: 1 report per resident per listing
            builder.Entity<MarketplaceListingReport>(entity =>
            {
                entity.HasIndex(r => new { r.ListingId, r.ReporterUserId }).IsUnique();

                entity.HasOne(r => r.Listing)
                      .WithMany(m => m.Reports)
                      .HasForeignKey(r => r.ListingId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(r => r.ReporterUser)
                      .WithMany()
                      .HasForeignKey(r => r.ReporterUserId)
                      .OnDelete(DeleteBehavior.Restrict);
            });
        }
    }
}
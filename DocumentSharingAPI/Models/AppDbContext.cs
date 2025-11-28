using Microsoft.EntityFrameworkCore;

namespace DocumentSharingAPI.Models
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        // Định nghĩa các bảng trong database
        public DbSet<User> Users { get; set; }
        public DbSet<Document> Documents { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<Comment> Comments { get; set; }
        public DbSet<UserDocument> UserDocuments { get; set; }
        public DbSet<Badge> Badges { get; set; }
        public DbSet<UserBadge> UserBadges { get; set; }
        public DbSet<Follow> Follows { get; set; }
        public DbSet<Notification> Notifications { get; set; }
        public DbSet<Post> Posts { get; set; }
        public DbSet<PostComment> PostComments { get; set; }
        public DbSet<Recommendation> Recommendations { get; set; }
        public DbSet<VipSubscription> VipSubscriptions { get; set; }
        public DbSet<Tag> Tags { get; set; }
        public DbSet<DocumentTag> DocumentTags { get; set; }
        public DbSet<Report> Reports { get; set; }
        public DbSet<Payment> Payments { get; set; }
        public DbSet<BankAccount> BankAccounts { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Cấu hình khóa chính ghép cho UserDocument
            modelBuilder.Entity<UserDocument>()
                .HasKey(ud => new { ud.UserId, ud.DocumentId, ud.ActionType });

            modelBuilder.Entity<UserDocument>()
                .HasOne(ud => ud.Document)
                .WithMany(d => d.UserDocuments)
                .HasForeignKey(ud => ud.DocumentId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<UserDocument>()
                .HasOne(ud => ud.User)
                .WithMany()
                .HasForeignKey(ud => ud.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            // Cấu hình khóa chính ghép cho UserBadge
            modelBuilder.Entity<UserBadge>()
                .HasKey(ub => new { ub.UserId, ub.BadgeId });

            // Quan hệ User-Document (một User có nhiều Document)
            modelBuilder.Entity<Document>()
                .HasOne(d => d.User)
                .WithMany(u => u.UploadedDocuments)
                .HasForeignKey(d => d.UploadedBy);

            // Quan hệ Document-Category (một Category có nhiều Document)
            modelBuilder.Entity<Document>()
                .HasOne(d => d.Category)
                .WithMany(c => c.Documents)
                .HasForeignKey(d => d.CategoryId);

            modelBuilder.Entity<Follow>()
                .HasOne(f => f.User) // người theo dõi
                .WithMany(u => u.Follows)
                .HasForeignKey(f => f.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Follow>()
                .HasOne(f => f.FollowedUser) // người bị theo dõi
                .WithMany(u => u.Followers)
                .HasForeignKey(f => f.FollowedUserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Comment>()
                .HasOne(c => c.Document)
                .WithMany(d => d.Comments)
                .HasForeignKey(c => c.DocumentId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Comment>()
                .HasOne(c => c.User)
                .WithMany()
                .HasForeignKey(c => c.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Recommendation>()
                .HasOne(r => r.Document)
                .WithMany()
                .HasForeignKey(r => r.DocumentId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Recommendation>()
                .HasOne(r => r.User)
                .WithMany()
                .HasForeignKey(r => r.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<PostComment>()
                .HasOne(pc => pc.Post)
                .WithMany(p => p.Comments)
                .HasForeignKey(pc => pc.PostId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<PostComment>()
                .HasOne(pc => pc.User)
                .WithMany()
                .HasForeignKey(pc => pc.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            // Cấu hình quan hệ VipSubscription-User
            modelBuilder.Entity<VipSubscription>()
                .HasOne(vs => vs.User)
                .WithMany(u => u.VipSubscriptions)
                .HasForeignKey(vs => vs.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Tag>(entity =>
            {
                entity.HasKey(t => t.TagId);
                entity.Property(t => t.Name).IsRequired().HasMaxLength(100);
                entity.HasIndex(t => t.Name).IsUnique(); 
            });

            modelBuilder.Entity<DocumentTag>(entity =>
            {
                entity.HasKey(dt => new { dt.DocumentId, dt.TagId });

                entity.HasOne(dt => dt.Document)
                    .WithMany(d => d.DocumentTags)
                    .HasForeignKey(dt => dt.DocumentId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(dt => dt.Tag)
                    .WithMany(t => t.DocumentTags)
                    .HasForeignKey(dt => dt.TagId)
                    .OnDelete(DeleteBehavior.Cascade);
                                                        
            });

            // Cấu hình quan hệ cho Report
            modelBuilder.Entity<Report>()
                .HasOne(r => r.Document)
                .WithMany()
                .HasForeignKey(r => r.DocumentId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Report>()
                .HasOne(r => r.User)
                .WithMany()
                .HasForeignKey(r => r.ReporterUserId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
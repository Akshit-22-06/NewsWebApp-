using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using NewsWebApp.Models;

namespace NewsWebApp.Data
{
    /// <summary>
    /// Application database context managing Identity and News domain entities.
    /// Configures entity mappings, relationships, indexes, and delete behaviors using Fluent API.
    /// </summary>
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<NewsArticle> NewsArticles { get; set; } = null!;
        public DbSet<Category> Categories { get; set; } = null!;
        public DbSet<Author> Authors { get; set; } = null!;
        public DbSet<Comment> Comments { get; set; } = null!;
        public DbSet<ArticleBookmark> ArticleBookmarks { get; set; } = null!;
        public DbSet<NewsletterSubscriber> NewsletterSubscribers { get; set; } = null!;
        public DbSet<NewsFeedSource> NewsFeedSources { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // ==========================================
            // Category Configuration
            // ==========================================
            builder.Entity<Category>(entity =>
            {
                entity.HasKey(c => c.Id);
                entity.Property(c => c.Name).IsRequired().HasMaxLength(100);
                entity.Property(c => c.Slug).IsRequired().HasMaxLength(100);
                entity.Property(c => c.Description).HasMaxLength(500);

                // Unique index on Slug for fast, unique URL lookups
                entity.HasIndex(c => c.Slug).IsUnique();

                // Relationship: 1 Category -> Many NewsArticles (Restrict delete to prevent accidental orphaned data)
                entity.HasMany(c => c.NewsArticles)
                      .WithOne(a => a.Category)
                      .HasForeignKey(a => a.CategoryId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // ==========================================
            // Author Configuration
            // ==========================================
            builder.Entity<Author>(entity =>
            {
                entity.HasKey(a => a.Id);
                entity.Property(a => a.DisplayName).IsRequired().HasMaxLength(100);
                entity.Property(a => a.Bio).HasMaxLength(500);
                entity.Property(a => a.ProfileImageUrl).HasMaxLength(250);

                // Relationship: 1 ApplicationUser -> 1 Author (Cascade delete)
                entity.HasOne(a => a.User)
                      .WithOne(u => u.Author)
                      .HasForeignKey<Author>(a => a.UserId)
                      .OnDelete(DeleteBehavior.Cascade);

                // Relationship: 1 Author -> Many NewsArticles (Restrict delete)
                entity.HasMany(a => a.NewsArticles)
                      .WithOne(n => n.Author)
                      .HasForeignKey(n => n.AuthorId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // ==========================================
            // NewsArticle Configuration
            // ==========================================
            builder.Entity<NewsArticle>(entity =>
            {
                entity.HasKey(a => a.Id);
                entity.Property(a => a.Title).IsRequired().HasMaxLength(200);
                entity.Property(a => a.Slug).IsRequired().HasMaxLength(250);
                entity.Property(a => a.Summary).IsRequired().HasMaxLength(500);
                entity.Property(a => a.Content).IsRequired();
                entity.Property(a => a.ImageUrl).HasMaxLength(500);

                // Unique index on Slug
                entity.HasIndex(a => a.Slug).IsUnique();

                // Composite index for querying published & featured articles quickly
                entity.HasIndex(a => new { a.IsPublished, a.PublishedDate });
                entity.HasIndex(a => new { a.IsPublished, a.IsFeatured });
                entity.HasIndex(a => a.Region);
                entity.HasIndex(a => a.LocationScope);

                // Relationship: 1 NewsArticle -> Many Comments (Cascade delete)
                entity.HasMany(a => a.Comments)
                      .WithOne(c => c.NewsArticle)
                      .HasForeignKey(c => c.NewsArticleId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // ==========================================
            // Comment Configuration
            // ==========================================
            builder.Entity<Comment>(entity =>
            {
                entity.HasKey(c => c.Id);
                entity.Property(c => c.Content).IsRequired().HasMaxLength(1000);

                // Relationship: ApplicationUser -> Comments (Restrict delete to prevent multiple cascade paths in SQL Server)
                entity.HasOne(c => c.User)
                      .WithMany(u => u.Comments)
                      .HasForeignKey(c => c.UserId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // ==========================================
            // ArticleBookmark Configuration
            // ==========================================
            builder.Entity<ArticleBookmark>(entity =>
            {
                entity.HasKey(b => b.Id);
                // Unique index to prevent duplicate bookmarks per user per article
                entity.HasIndex(b => new { b.UserId, b.NewsArticleId }).IsUnique();

                entity.HasOne(b => b.User)
                      .WithMany(u => u.Bookmarks)
                      .HasForeignKey(b => b.UserId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(b => b.NewsArticle)
                      .WithMany(a => a.Bookmarks)
                      .HasForeignKey(b => b.NewsArticleId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // ==========================================
            // NewsletterSubscriber Configuration
            // ==========================================
            builder.Entity<NewsletterSubscriber>(entity =>
            {
                entity.HasKey(s => s.Id);
                entity.Property(s => s.Email).IsRequired().HasMaxLength(256);
                entity.HasIndex(s => s.Email).IsUnique();
            });

            // ==========================================
            // NewsFeedSource Configuration
            // ==========================================
            builder.Entity<NewsFeedSource>(entity =>
            {
                entity.HasKey(s => s.Id);
                entity.Property(s => s.Name).IsRequired().HasMaxLength(100);
                entity.Property(s => s.FeedUrl).IsRequired().HasMaxLength(500);
                entity.Property(s => s.CategorySlug).IsRequired().HasMaxLength(100);
                entity.HasIndex(s => s.FeedUrl).IsUnique();
            });
        }
    }
}

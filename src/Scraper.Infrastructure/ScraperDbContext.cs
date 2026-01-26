using Microsoft.EntityFrameworkCore;
using Scraper.Core.Models;

namespace Scraper.Infrastructure;

public class ScraperDbContext : DbContext
{
    public ScraperDbContext(DbContextOptions<ScraperDbContext> options) : base(options)
    {
    }

    public DbSet<OtpChallenge> OtpChallenges { get; set; }
    public DbSet<ScrapeResult> ScrapeResults { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<OtpChallenge>(entity =>
        {
            entity.ToTable("OtpChallenge");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasDefaultValueSql("NEWID()");
            entity.Property(e => e.AccountId).HasMaxLength(100).IsRequired();
            entity.Property(e => e.CorrelationId).HasMaxLength(200);
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .IsRequired()
                .HasConversion(
                    v => v.ToString(),
                    v => (OtpChallengeStatus)Enum.Parse(typeof(OtpChallengeStatus), v));
            entity.Property(e => e.Code).HasMaxLength(10);
            entity.Property(e => e.ExpiresAt).IsRequired();
            entity.Property(e => e.CreatedAt).IsRequired().HasDefaultValueSql("GETUTCDATE()");
            entity.Property(e => e.UsedAt);

            entity.HasIndex(e => e.AccountId);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.ExpiresAt);
        });

        modelBuilder.Entity<ScrapeResult>(entity =>
        {
            entity.ToTable("ScrapeResult");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasDefaultValueSql("NEWID()");
            entity.Property(e => e.AccountId).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Url).HasMaxLength(2000).IsRequired();
            entity.Property(e => e.CapturedAt).IsRequired().HasDefaultValueSql("GETUTCDATE()");
            entity.Property(e => e.PayloadJson).HasColumnType("NVARCHAR(MAX)");
            entity.Property(e => e.ContentHash).HasMaxLength(64);
            entity.Property(e => e.HtmlSnapshotPath).HasMaxLength(500);

            entity.HasIndex(e => e.AccountId);
            entity.HasIndex(e => e.CapturedAt);
            entity.HasIndex(e => e.ContentHash);
        });
    }
}

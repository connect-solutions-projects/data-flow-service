using System.Text.Json;
using DataFlow.Core.Domain.Entities;
using DataFlow.Core.Domain.Enums;
using DataFlow.Core.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace DataFlow.Infrastructure.Persistence;

public class IngestionDbContext : DbContext
{
    public IngestionDbContext(DbContextOptions<IngestionDbContext> options) : base(options) {}

    public DbSet<IngestionJob> IngestionJobs => Set<IngestionJob>();
    public DbSet<Client> Clients => Set<Client>();
    public DbSet<ClientPolicy> ClientPolicies => Set<ClientPolicy>();
    public DbSet<WebhookSubscription> WebhookSubscriptions => Set<WebhookSubscription>();
    public DbSet<ImportBatch> ImportBatches => Set<ImportBatch>();
    public DbSet<ImportItem> ImportItems => Set<ImportItem>();
    public DbSet<BatchLock> BatchLocks => Set<BatchLock>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var job = modelBuilder.Entity<IngestionJob>();
        job.HasKey(j => j.Id);

        job.Property(j => j.ClientId).IsRequired();
        job.Property(j => j.Status)
            .HasConversion<string>()
            .IsRequired();

        job.Property(j => j.FileType)
            .HasConversion<string>()
            .IsRequired();

        job.Property(j => j.CreatedAt).IsRequired();
        job.Property(j => j.StartedAt);
        job.Property(j => j.CompletedAt);
        job.Property(j => j.ErrorMessage);
        job.Property(j => j.RetryCount).IsRequired();
        job.Property(j => j.MaxRetries).IsRequired();

        // FileMetadata como owned type (flatten columns)
        job.OwnsOne(j => j.FileMetadata, fm =>
        {
            fm.Property(p => p.Name).IsRequired();
            fm.Property(p => p.Size).IsRequired();
            fm.Property(p => p.ContentType).IsRequired();
            fm.Property(p => p.Checksum).IsRequired();
            fm.Property(p => p.UploadedAt).IsRequired();
        });

        // ValidationResult armazenado como JSONB
        var validationResultConverter = new ValueConverter<ValidationResult?, string?>(
            v => v == null ? null : JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
            v => v == null ? null : JsonSerializer.Deserialize<ValidationResult>(v, (JsonSerializerOptions?)null)
        );

        job.Property(j => j.ValidationResult)
           .HasConversion(validationResultConverter)
           .HasColumnType("nvarchar(max)");

        ConfigureClients(modelBuilder);
        ConfigureClientPolicies(modelBuilder);
        ConfigureImportBatches(modelBuilder);
        ConfigureImportItems(modelBuilder);
        ConfigureWebhookSubscriptions(modelBuilder);
        ConfigureBatchLock(modelBuilder);
    }

    private static void ConfigureClients(ModelBuilder modelBuilder)
    {
        var client = modelBuilder.Entity<Client>();
        client.HasKey(c => c.Id);
        client.HasIndex(c => c.ClientIdentifier).IsUnique();
        client.Property(c => c.Name).HasMaxLength(120).IsRequired();
        client.Property(c => c.ClientIdentifier).HasMaxLength(64).IsRequired();
        client.Property(c => c.SecretHash).IsRequired();
        client.Property(c => c.SecretSalt).IsRequired();
        client.Property(c => c.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
        client.Property(c => c.CreatedAt).IsRequired();
    }

    private static void ConfigureClientPolicies(ModelBuilder modelBuilder)
    {
        var policy = modelBuilder.Entity<ClientPolicy>();
        policy.HasKey(p => p.Id);
        policy.Property(p => p.CreatedAt).IsRequired();
        policy.Property(p => p.MaxFileSizeMb);
        policy.Property(p => p.MaxBatchPerDay);
        policy.Property(p => p.AllowedStartHour);
        policy.Property(p => p.AllowedEndHour);
        policy.Property(p => p.LargeThresholdMb);
        policy.Property(p => p.RequireSchedulingForLarge);
        policy.Property(p => p.RateLimitPerMinute);
        policy.Property(p => p.RedactPayloadOnSuccess);
        policy.Property(p => p.RedactPayloadOnFailure);
        policy.Property(p => p.RetentionDays);
        policy.HasOne(p => p.Client)
            .WithMany(c => c.Policies)
            .HasForeignKey(p => p.ClientId);
    }

    private static void ConfigureImportBatches(ModelBuilder modelBuilder)
    {
        var batch = modelBuilder.Entity<ImportBatch>();
        batch.HasKey(b => b.Id);
        batch.Property(b => b.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
        batch.Property(b => b.FileType).HasConversion<string>().HasMaxLength(16).IsRequired();
        batch.Property(b => b.FileName).HasMaxLength(260).IsRequired();
        batch.Property(b => b.Checksum).HasMaxLength(128).IsRequired();
        batch.Property(b => b.UploadPath).HasMaxLength(512).IsRequired();
        batch.Property(b => b.PolicyDecision).HasMaxLength(64).IsRequired();
        batch.Property(b => b.OriginDefault).HasMaxLength(120);
        batch.Property(b => b.RequestedBy).HasMaxLength(120);
        batch.Property(b => b.MetadataJson).HasColumnType("nvarchar(max)");
        batch.Property(b => b.ErrorSummary).HasColumnType("nvarchar(max)");
        batch.HasOne(b => b.Client)
            .WithMany(c => c.ImportBatches)
            .HasForeignKey(b => b.ClientId);

        batch.HasMany(b => b.Items)
            .WithOne(i => i.Batch)
            .HasForeignKey(i => i.BatchId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    private static void ConfigureImportItems(ModelBuilder modelBuilder)
    {
        var item = modelBuilder.Entity<ImportItem>();
        item.HasKey(i => i.Id);
        item.Property(i => i.Id).ValueGeneratedOnAdd();
        item.Property(i => i.PayloadJson).HasColumnType("nvarchar(max)");
        item.Property(i => i.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
        item.Property(i => i.CreatedAt).IsRequired();
        item.Property(i => i.Sequence).IsRequired();
        item.HasOne(i => i.Batch)
            .WithMany(b => b.Items)
            .HasForeignKey(i => i.BatchId);
    }

    private static void ConfigureWebhookSubscriptions(ModelBuilder modelBuilder)
    {
        var webhook = modelBuilder.Entity<WebhookSubscription>();
        webhook.HasKey(w => w.Id);
        webhook.Property(w => w.Url).HasMaxLength(512).IsRequired();
        webhook.Property(w => w.Secret).HasMaxLength(128);
        webhook.HasIndex(w => new { w.ClientId, w.Url }).IsUnique();
        webhook.HasOne(w => w.Client)
            .WithMany(c => c.WebhookSubscriptions)
            .HasForeignKey(w => w.ClientId);
    }

    private static void ConfigureBatchLock(ModelBuilder modelBuilder)
    {
        var lockEntity = modelBuilder.Entity<BatchLock>();
        lockEntity.HasKey(l => l.Id);
        lockEntity.Property(l => l.Id).ValueGeneratedNever();
        lockEntity.HasData(new { Id = 1, IsLocked = false, LockOwnerBatchId = (Guid?)null, LockedAt = (DateTime?)null });
    }
}


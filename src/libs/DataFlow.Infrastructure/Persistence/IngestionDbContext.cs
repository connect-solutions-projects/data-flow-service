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
           .HasColumnType("jsonb");
    }
}


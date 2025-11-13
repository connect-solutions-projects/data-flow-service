using System;
using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace DataFlow.Infrastructure.Persistence.DesignTime
{
    public class IngestionDbContextFactory : IDesignTimeDbContextFactory<IngestionDbContext>
    {
        public IngestionDbContext CreateDbContext(string[] args)
        {
            // Prefer environment variable; fallback to local default
            var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DataFlow")
                                   ?? "Host=localhost;Port=15432;Database=dataflow_db;Username=postgres;Password=postgres";

            var optionsBuilder = new DbContextOptionsBuilder<IngestionDbContext>();
            optionsBuilder.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "public");
            });

            return new IngestionDbContext(optionsBuilder.Options);
        }
    }
}

using System;
using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace DataFlow.Infrastructure.Persistence.DesignTime;

/// <summary>
/// Factory para criação do DbContext em design-time (migrations).
/// </summary>
public class IngestionDbContextFactory : IDesignTimeDbContextFactory<IngestionDbContext>
{
    public IngestionDbContext CreateDbContext(string[] args)
    {
        var basePath = LocateAppSettingsDirectory();

        var configuration = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("DataFlow");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "ConnectionStrings:DataFlow não encontrada. Configure em appsettings.json ou variáveis de ambiente.");
        }

        var optionsBuilder = new DbContextOptionsBuilder<IngestionDbContext>();
        optionsBuilder.UseSqlServer(connectionString, sql =>
        {
            sql.MigrationsHistoryTable("__EFMigrationsHistory");
            sql.EnableRetryOnFailure(
                maxRetryCount: 3,
                maxRetryDelay: TimeSpan.FromSeconds(5),
                errorNumbersToAdd: null);
        });

        return new IngestionDbContext(optionsBuilder.Options);
    }

    private static string LocateAppSettingsDirectory()
    {
        var directory = Directory.GetCurrentDirectory();
        while (!string.IsNullOrEmpty(directory))
        {
            var candidate = Path.Combine(directory, "appsettings.json");
            if (File.Exists(candidate))
            {
                return directory;
            }
            directory = Directory.GetParent(directory)?.FullName;
        }

        throw new InvalidOperationException("Não foi possível localizar appsettings.json em nenhum diretório pai.");
    }
}

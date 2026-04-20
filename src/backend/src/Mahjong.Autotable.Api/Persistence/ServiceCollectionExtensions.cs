using Mahjong.Autotable.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Mahjong.Autotable.Api.Persistence;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<PersistenceOptions>(configuration.GetSection("Persistence"));

        var provider = configuration.GetValue<string>("Persistence:Provider") ?? PersistenceProvider.Sqlite.ToString();

        services.AddDbContext<AppDbContext>(options =>
        {
            switch (provider.ToLowerInvariant())
            {
                case "postgresql":
                case "postgres":
                    options.UseNpgsql(configuration.GetConnectionString("PostgreSql")
                        ?? throw new InvalidOperationException("ConnectionStrings:PostgreSql is required for PostgreSql provider."));
                    break;

                case "sqlserver":
                    options.UseSqlServer(configuration.GetConnectionString("SqlServer")
                        ?? throw new InvalidOperationException("ConnectionStrings:SqlServer is required for SqlServer provider."));
                    break;

                default:
                    options.UseSqlite(configuration.GetConnectionString("Sqlite")
                        ?? "Data Source=data/mahjong-autotable.db");
                    break;
            }
        });

        return services;
    }
}

using LinqToDB;
using LinqToDB.AspNet;
using LinqToDB.AspNet.Logging;
using Microsoft.Extensions.DependencyInjection;
using SpoolManager.Infrastructure.Data;
using SpoolManager.Infrastructure.Data.Mappings;

namespace SpoolManager.Infrastructure;

public static class ServiceExtensions
{
    public static IServiceCollection AddSpoolManagerDb(this IServiceCollection services, string connectionString)
    {
        services.AddLinqToDBContext<SpoolManagerDb>((provider, options) =>
            options
                .UseMySqlConnector(connectionString, _ => _)
                .UseMappingSchema(SpoolManagerMappings.Build())
                .UseDefaultLogging(provider));
        return services;
    }
}

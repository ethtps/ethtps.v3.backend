using ETHTPS.V3.Data;

using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using static ETHTPS.Utils.Configuration.Enums;

namespace ETHTPS.V3.DependencyInjection
{
    /// <summary>
    /// Represents a class that contains extension methods for the database.
    /// </summary>
    public static class DatabaseExtensions
    {
        public static void ConfigureDatabase(this IServiceCollection services, IConfiguration configuration, ETHTPSEnvironment currentEnvironment)
        {
            services.AddDbContext<ETHTPSContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString(currentEnvironment.ToString())));
        }

        /// <summary>
        /// Configures the database.
        /// </summary>
        /// <param name="services">The services collection.</param>
        /// <param name="connectionString">The connection string.</param>
        private static void ConfigureDatabase(this IServiceCollection services, string connectionString)
        {
            services.AddDbContext<ETHTPSContext>(options =>
            options.UseSqlServer(connectionString));
        }

        /// <summary>
        /// Migrates the database.
        /// </summary>
        /// <param name="app">The application builder.</param>
        public static void MigrateDatabase(this IApplicationBuilder app)
        {
            using (var scope = app.ApplicationServices.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<ETHTPSContext>();
                dbContext.Database.Migrate();
            }
        }
    }
}

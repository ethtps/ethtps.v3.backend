using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace ETHTPS.V3.Data
{
    public class ETHTPSContextFactory : IDesignTimeDbContextFactory<ETHTPSContext>
    {
        public ETHTPSContext CreateDbContext(string[] args)
        {
            IConfigurationRoot configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.development.json")
                .Build();

            var optionsBuilder = new DbContextOptionsBuilder<ETHTPSContext>();
            var connectionString = configuration.GetConnectionString("DefaultConnection");
            optionsBuilder.UseSqlServer(connectionString);

            return new ETHTPSContext(optionsBuilder.Options);
        }
    }
}

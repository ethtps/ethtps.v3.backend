using ETHTPS.V3.Data.Models;

using Microsoft.EntityFrameworkCore;

namespace ETHTPS.V3.Data
{
    public class ETHTPSContext : DbContext
    {
        public ETHTPSContext(DbContextOptions<ETHTPSContext> options) : base(options)
        {
        }

        public DbSet<Updater> Updaters { get; set; }
        public DbSet<UpdaterConfiguration> UpdaterConfigurations { get; set; }
        public DbSet<Endpoint> Endpoints { get; set; }
        public DbSet<Binding> Bindings { get; set; }
        public DbSet<HealthStatus> HealthStatuses { get; set; }
        public DbSet<Health> Health { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {

        }
    }

}

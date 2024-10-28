using ETHTPS.V3.Data.Models;

using Microsoft.EntityFrameworkCore;

namespace ETHTPS.V3.Data
{
    /// <summary>
    /// Represents the database context for the ETHTPS application.
    /// </summary>
    public class ETHTPSContext : DbContext
    {
        public ETHTPSContext() { }

        public ETHTPSContext(DbContextOptions<ETHTPSContext> options) : base(options)
        {

        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            base.OnConfiguring(optionsBuilder);
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

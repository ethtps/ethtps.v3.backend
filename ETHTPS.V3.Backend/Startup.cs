using ETHTPS.Utils.Constants;
using ETHTPS.V3.DependencyInjection;

using static ETHTPS.Utils.Configuration.Enums;

namespace ETHTPS.V3.Backend
{
    public class Startup
    {
        public IConfiguration Configuration { get; }
        private static ETHTPSEnvironment CurrentEnvironment { get => Constants.CURRENT_ENVIRONMENT; }

        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddSwaggerGen(setupAction =>
            {
                setupAction.SwaggerDoc("v3", new Microsoft.OpenApi.Models.OpenApiInfo() { Title = "ETHTPS API", Version = "v3" });
            });
            services.ConfigureDatabase(Configuration, CurrentEnvironment);
            services.AddChainlistClient(Configuration, CurrentEnvironment);
            services.AddControllers();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
                app.UseHsts();
            }

            app.UseSwagger();
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v3/swagger.json", "ETHTPS API V3");
            });
            app.MigrateDatabase();
            app.UseHttpsRedirection();
            app.UseStaticFiles();
            app.UseRouting();
            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }
    }
}

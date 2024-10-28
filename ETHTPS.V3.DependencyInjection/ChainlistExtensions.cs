using Chainlist.API;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using static ETHTPS.Utils.Configuration.Enums;

namespace ETHTPS.V3.DependencyInjection
{
    /// <summary>
    /// Represents a class that contains extension methods for the Chainlist API.
    /// </summary>
    public static class ChainlistExtensions
    {
        /// <summary>
        /// Configures the Chainlist API.
        /// </summary>
        /// <param name="services"></param>
        /// <param name="configuration"></param>
        /// <param name="currentEnvironment"></param>
        public static void AddChainlistClient(this IServiceCollection services, IConfiguration configuration, ETHTPSEnvironment currentEnvironment)
        {
            var baseUrl = configuration.GetSection("Chainlist").GetValue<string>(currentEnvironment.ToString())!;
            services.AddChainlistClient(baseUrl);
        }

        /// <summary>
        /// Configures the Chainlist API.
        /// </summary>
        /// <param name="services"></param>
        /// <param name="baseUrl"></param>

        private static void AddChainlistClient(this IServiceCollection services, string baseUrl)
        {
            services.AddHttpClient<ChainlistClient>(client =>
            {
                client.BaseAddress = new Uri(baseUrl);
            });
        }
    }
}

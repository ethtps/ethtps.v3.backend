using static ETHTPS.Utils.Configuration.Enums;

namespace ETHTPS.Utils.Constants
{
    /// <summary>
    /// Constants used in development
    /// </summary>
    public class DevelopmentConstants : SharedConstants
    {
        /// <summary>
        /// The name of the SQL Server connection string.
        /// </summary>
        public static string SQL_SERVER_CONNECTION_STRING_NAME = "DevelopmentServer";

        /// <summary>
        /// The current environment.
        /// </summary>
        public static ETHTPSEnvironment CURRENT_ENVIRONMENT = ETHTPSEnvironment.Development;
    }
}
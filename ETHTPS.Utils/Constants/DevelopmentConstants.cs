using static ETHTPS.Utils.Configuration.Enums;

namespace ETHTPS.Utils.Constants
{
    /// <summary>
    /// Constants used in development
    /// </summary>
    public class DevelopmentConstants : SharedConstants
    {
        public static string SQL_SERVER_CONNECTION_STRING_NAME = "DevelopmentServer";
        public static ETHTPSEnvironment CURRENT_ENVIRONMENT = ETHTPSEnvironment.Development;
    }
}
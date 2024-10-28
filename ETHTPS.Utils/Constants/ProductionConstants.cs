using static ETHTPS.Utils.Configuration.Enums;

namespace ETHTPS.Utils.Constants
{
    /// <summary>
    /// Constants used in production
    /// </summary>
    public class ProductionConstants : SharedConstants
    {
        public static string SQL_SERVER_CONNECTION_STRING_NAME = "ProductionServer";
        public static ETHTPSEnvironment CURRENT_ENVIRONMENT = ETHTPSEnvironment.Production;
    }
}

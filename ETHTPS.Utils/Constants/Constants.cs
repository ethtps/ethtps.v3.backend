namespace ETHTPS.Utils.Constants
{
    public sealed class Constants :
#if DEBUG
        DevelopmentConstants
#else
        ProductionConstants
#endif
    {

    }
}

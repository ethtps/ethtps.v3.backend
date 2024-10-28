namespace ETHTPS.Utils.Constants
{
    public sealed class Constants :
#if DEBUG
        SharedConstants
#else
        ProductionConstants
#endif
    {

    }
}

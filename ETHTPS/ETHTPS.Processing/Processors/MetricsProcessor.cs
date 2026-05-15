using ETHTPS.Processing.Models;

namespace ETHTPS.Processing.Processors;

public static class MetricsProcessor
{
    public static ComputedMetrics Compute(RawBlock block)
    {
        double? tps = null, gps = null;
        if (block.BlockTimeMs > 0)
        {
            var seconds = block.BlockTimeMs / 1000.0;
            tps = block.TransactionCount / seconds;
            gps = (double)block.GasUsed / seconds;
        }
        return new ComputedMetrics
        {
            ChainId = block.ChainId,
            BlockNumber = block.BlockNumber,
            Timestamp = block.Timestamp,
            Tps = tps,
            Gps = gps
        };
    }
}

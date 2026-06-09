using ETHTPS.Backfill.Models;

namespace ETHTPS.Backfill.Crawlers;

public record BlockWindow(
    RawBlock Block,
    List<RawTransaction> Transactions,
    ComputedMetrics Metrics);

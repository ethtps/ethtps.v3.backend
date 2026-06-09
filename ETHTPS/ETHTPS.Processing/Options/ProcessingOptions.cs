namespace ETHTPS.Processing.Options;

public class ProcessingOptions
{
    public int ShardChainIdMin { get; set; } = 1;
    public int ShardChainIdMax { get; set; } = int.MaxValue;
    public int TransactionBatchSize { get; set; } = 1000;
    public int TransactionFlushIntervalMs { get; set; } = 500;
    public int TransactionChannelCapacity { get; set; } = 50_000;
    public int RedisLiveTtlSeconds { get; set; } = 60;
}

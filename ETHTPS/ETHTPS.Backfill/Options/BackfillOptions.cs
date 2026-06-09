namespace ETHTPS.Backfill.Options;

public class BackfillOptions
{
    public int ShardChainIdMin { get; set; } = 1;
    public int ShardChainIdMax { get; set; } = int.MaxValue;
    public int MaxConcurrentChains { get; set; } = 50;
    public int MaxConcurrentBlocksPerChain { get; set; } = 10;
    public int CheckpointIntervalBlocks { get; set; } = 100;
    public int RpcTimeoutMs { get; set; } = 10000;
    public int MaxRpcBackoffSeconds { get; set; } = 60;
    public int TransactionBatchSize { get; set; } = 1000;
    public int LookbackHours { get; set; } = 1;
}

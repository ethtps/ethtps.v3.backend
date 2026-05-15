namespace ETHTPS.Ingestion.Options;

public class IngestionOptions
{
    public int ShardChainIdMin { get; set; } = 1;
    public int ShardChainIdMax { get; set; } = int.MaxValue;
    public int PollIntervalMs { get; set; } = 2000;
    public int RpcTimeoutMs { get; set; } = 5000;
    public int MaxRpcBackoffSeconds { get; set; } = 60;
}

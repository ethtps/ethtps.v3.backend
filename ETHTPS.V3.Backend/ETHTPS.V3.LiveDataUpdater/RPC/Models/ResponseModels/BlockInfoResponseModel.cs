using Newtonsoft.Json;

namespace ETHTPS.V3.LiveDataUpdater.RPC.Models.ResponseModels
{
    public class BlockInfoResponseModel
    {
        [JsonProperty("jsonrpc")]
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
        public string JsonRpc { get; set; }

        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("result")]
        public BlockResult? Result { get; set; }
    }

    public class BlockResult
    {
        [JsonProperty("baseFeePerGas")]
        public string BaseFeePerGas { get; set; }

        [JsonProperty("blobGasUsed")]
        public string BlobGasUsed { get; set; }

        [JsonProperty("difficulty")]
        public string Difficulty { get; set; }

        [JsonProperty("excessBlobGas")]
        public string ExcessBlobGas { get; set; }

        [JsonProperty("extraData")]
        public string ExtraData { get; set; }

        [JsonProperty("gasLimit")]
        public string GasLimit { get; set; }

        [JsonProperty("gasUsed")]
        public string GasUsed { get; set; }

        [JsonProperty("hash")]
        public string Hash { get; set; }

        [JsonProperty("logsBloom")]
        public string LogsBloom { get; set; }

        [JsonProperty("miner")]
        public string Miner { get; set; }

        [JsonProperty("mixHash")]
        public string MixHash { get; set; }

        [JsonProperty("nonce")]
        public string Nonce { get; set; }

        [JsonProperty("number")]
        public string Number { get; set; }

        [JsonProperty("parentBeaconBlockRoot")]
        public string ParentBeaconBlockRoot { get; set; }

        [JsonProperty("parentHash")]
        public string ParentHash { get; set; }

        [JsonProperty("receiptsRoot")]
        public string ReceiptsRoot { get; set; }

        [JsonProperty("requestsHash")]
        public string RequestsHash { get; set; }

        [JsonProperty("sha3Uncles")]
        public string Sha3Uncles { get; set; }

        [JsonProperty("size")]
        public string Size { get; set; }

        [JsonProperty("stateRoot")]
        public string StateRoot { get; set; }

        [JsonProperty("timestamp")]
        public string Timestamp { get; set; }

        [JsonProperty("transactions")]
        public List<string> Transactions { get; set; }

        [JsonProperty("transactionsRoot")]
        public string TransactionsRoot { get; set; }

        [JsonProperty("uncles")]
        public List<string> Uncles { get; set; }

        [JsonProperty("withdrawals")]
        public List<Withdrawal> Withdrawals { get; set; }

        [JsonProperty("withdrawalsRoot")]
        public string WithdrawalsRoot { get; set; }
    }

    public class Withdrawal
    {
        [JsonProperty("address")]
        public string Address { get; set; }

        [JsonProperty("amount")]
        public string Amount { get; set; }

        [JsonProperty("index")]
        public string Index { get; set; }

        [JsonProperty("validatorIndex")]
        public string ValidatorIndex { get; set; }
    }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
}

using Newtonsoft.Json;

namespace ETHTPS.V3.LiveDataUpdater.RPC.Models.RequestModels
{
    public abstract class RPCRequestModelBase
    {
        protected RPCRequestModelBase(string jSONRPC, string method, object[]? @params, int iD)
        {
            JSONRPC = jSONRPC;
            Method = method;
            Params = @params;
            ID = iD;
        }

        [JsonProperty("jsonrpc")]
        public string JSONRPC { get; set; }

        [JsonProperty("method")]
        public string Method { get; set; }

        [JsonProperty("_params")]
        public object[]? Params { get; set; }

        [JsonProperty("id")]
        public int ID { get; set; }
    }
}

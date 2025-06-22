namespace ETHTPS.V3.ChainDataUpdater
{
    public class ChainlistResponse
    {
        public ChainInfo[] Data { get; set; }
    }

    public class ChainInfo
    {
        public string name { get; set; }
        public string chain { get; set; }
        public string icon { get; set; }
        public Rpc[] rpc { get; set; }
        public Feature[] features { get; set; }
        public string[] faucets { get; set; }
        public Nativecurrency nativeCurrency { get; set; }
        public string infoURL { get; set; }
        public string shortName { get; set; }
        public long chainId { get; set; }
        public long networkId { get; set; }
        public long slip44 { get; set; }
        public Ens ens { get; set; }
        public Explorer[] explorers { get; set; }
        public float tvl { get; set; }
        public string chainSlug { get; set; }
        public string status { get; set; }
        public Parent parent { get; set; }
        public string title { get; set; }
        public string[] redFlags { get; set; }
    }

    public class Nativecurrency
    {
        public string name { get; set; }
        public string symbol { get; set; }
        public int decimals { get; set; }
    }

    public class Ens
    {
        public string registry { get; set; }
    }

    public class Parent
    {
        public string type { get; set; }
        public string chain { get; set; }
        public Bridge[] bridges { get; set; }
    }

    public class Bridge
    {
        public string url { get; set; }
    }

    public class Rpc
    {
        public string url { get; set; }
        public string tracking { get; set; }
        public bool isOpenSource { get; set; }
    }

    public class Feature
    {
        public string name { get; set; }
    }

    public class Explorer
    {
        public string name { get; set; }
        public string url { get; set; }
        public string standard { get; set; }
        public string icon { get; set; }
    }

}

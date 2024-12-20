using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace ETHTPS.V3.Data.Mongo.Models
{
    public class ChainInfo
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        [BsonElement("name")]
        public string? Name { get; set; }
        [BsonElement("title")]
        public string? Title { get; set; }

        [BsonElement("chain")]
        public string? Chain { get; set; }

        [BsonElement("icon")]
        public string? Icon { get; set; }

        [BsonElement("rpc")]
        public List<string?>? Rpc { get; set; }

        [BsonElement("features")]
        public List<Feature?>? Features { get; set; }

        [BsonElement("faucets")]
        public List<string?>? Faucets { get; set; }

        [BsonElement("nativeCurrency")]
        public NativeCurrency? NativeCurrency { get; set; }

        [BsonElement("infoURL")]
        public string? InfoURL { get; set; }

        [BsonElement("shortName")]
        public string? ShortName { get; set; }

        [BsonElement("chainId")]
        public long ChainId { get; set; }

        [BsonElement("networkId")]
        public long NetworkId { get; set; }

        [BsonElement("slip44")]
        public long Slip44 { get; set; }
        [BsonElement(elementName: "status")]
        public string? Status { get; set; }
        [BsonElement(elementName: "redFlags")]
        public string[]? RedFlags { get; set; }
        [BsonElement(elementName: "parent")]
        public object? Parent { get; set; }


        [BsonElement("ens")]
        public Ens? Ens { get; set; }

        [BsonElement("explorers")]
        public List<Explorer?>? Explorers { get; set; }
    }

    public class Feature
    {
        [BsonElement("name")]
        public string? Name { get; set; }
    }

    public class NativeCurrency
    {
        [BsonElement("name")]
        public string? Name { get; set; }

        [BsonElement("symbol")]
        public string? Symbol { get; set; }

        [BsonElement("decimals")]
        public int? Decimals { get; set; }
    }

    public class Ens
    {
        [BsonElement("registry")]
        public string? Registry { get; set; }
    }

    public class Explorer
    {
        [BsonElement("name")]
        public string? Name { get; set; }

        [BsonElement("url")]
        public string? Url { get; set; }

        [BsonElement("icon")]
        public string? Icon { get; set; }

        [BsonElement("standard")]
        public string? Standard { get; set; }
    }
}
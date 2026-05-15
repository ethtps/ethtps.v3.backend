namespace ETHTPS.API.Models.Responses;

public record NetworkResponse(int ChainId, string Name, string[] RpcUrls, bool Enabled);

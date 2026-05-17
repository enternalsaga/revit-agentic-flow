using Newtonsoft.Json;

namespace RevitMcpSdk.Models;

public class JsonRpcSuccessResponse
{
    [JsonProperty("jsonrpc")]
    public string JsonRpc { get; set; } = "2.0";

    [JsonProperty("id")]
    public string Id { get; set; } = "";

    [JsonProperty("result")]
    public object? Result { get; set; }
}

public class JsonRpcErrorResponse
{
    [JsonProperty("jsonrpc")]
    public string JsonRpc { get; set; } = "2.0";

    [JsonProperty("id")]
    public string Id { get; set; } = "";

    [JsonProperty("error")]
    public JsonRpcError Error { get; set; } = new();
}

public class JsonRpcError
{
    [JsonProperty("code")]
    public int Code { get; set; }

    [JsonProperty("message")]
    public string Message { get; set; } = "";

    [JsonProperty("data")]
    public object? Data { get; set; }
}

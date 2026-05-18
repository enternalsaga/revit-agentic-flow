using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RevitMcpSdk.Models;

namespace RevitMcpPlugin.Core;

public class CommandExecutor
{
    private const int ParseError = -32700;
    private const int InvalidRequest = -32600;
    private const int MethodNotFound = -32601;
    private const int InternalError = -32603;

    private readonly ICommandRegistry _registry;

    public CommandExecutor(ICommandRegistry registry)
    {
        _registry = registry;
    }

    public string Execute(JsonRpcRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Method))
            return CreateErrorResponse(request.Id, InvalidRequest, "Invalid JSON-RPC request: method is required.");

        if (!_registry.TryGetCommand(request.Method, out var command))
            return CreateErrorResponse(request.Id, MethodNotFound, $"Method '{request.Method}' not found.");

        try
        {
            var result = command.Execute(request.GetParamsObject(), request.Id);
            return CreateSuccessResponse(request.Id, result);
        }
        catch (Exception ex)
        {
            return CreateErrorResponse(request.Id, InternalError, ex.Message);
        }
    }

    public string ExecuteJson(string requestJson)
    {
        try
        {
            var request = JsonConvert.DeserializeObject<JsonRpcRequest>(requestJson);
            return request == null
                ? CreateErrorResponse("", InvalidRequest, "Invalid JSON-RPC request.")
                : Execute(request);
        }
        catch (JsonException)
        {
            return CreateErrorResponse("", ParseError, "Invalid JSON.");
        }
        catch (Exception ex)
        {
            return CreateErrorResponse("", InternalError, ex.Message);
        }
    }

    private static string CreateSuccessResponse(string id, object? result)
    {
        var response = new JsonRpcSuccessResponse
        {
            Id = id,
            Result = result is JToken token ? token : result
        };

        return JsonConvert.SerializeObject(response);
    }

    private static string CreateErrorResponse(string id, int code, string message, object? data = null)
    {
        var response = new JsonRpcErrorResponse
        {
            Id = id,
            Error = new JsonRpcError
            {
                Code = code,
                Message = message,
                Data = data
            }
        };

        return JsonConvert.SerializeObject(response);
    }
}

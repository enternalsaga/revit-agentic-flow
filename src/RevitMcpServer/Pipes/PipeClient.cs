using System.IO.Pipes;
using System.Text.Json;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RevitMcpSdk.Models;

namespace RevitMcpServer.Pipes;

public class PipeClient : IDisposable
{
    private readonly string _pipeName;
    private readonly int _timeoutMs;

    public PipeClient(string pipeName, int timeoutMs = 120_000)
    {
        _pipeName = pipeName;
        _timeoutMs = timeoutMs;
    }

    public async Task<string> SendCommandAsync(string command, object? parameters = null, CancellationToken ct = default)
    {
        using var pipe = new NamedPipeClientStream(".", _pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        await pipe.ConnectAsync(5000, ct);

        var request = new JsonRpcRequest
        {
            Method = command,
            Params = ToJToken(parameters),
            Id = $"{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}{Random.Shared.Next(100000, 999999)}"
        };

        var requestJson = JsonConvert.SerializeObject(request);
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(_timeoutMs);

        try
        {
            await PipeProtocol.WriteMessageAsync(pipe, requestJson, timeoutCts.Token);

            var responseJson = await PipeProtocol.ReadMessageAsync(pipe, timeoutCts.Token)
                ?? throw new TimeoutException($"No response from Revit for command '{command}' within {_timeoutMs}ms");

            var response = JObject.Parse(responseJson);
            if (response.TryGetValue("error", out var errorToken) && errorToken.Type != JTokenType.Null)
            {
                var error = errorToken.ToObject<JsonRpcError>();
                var message = string.IsNullOrWhiteSpace(error?.Message)
                    ? "Unknown Revit error"
                    : error.Message;
                throw new Exception($"Revit error ({error?.Code ?? 0}): {message}");
            }

            return responseJson;
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !ct.IsCancellationRequested)
        {
            throw new TimeoutException($"Timed out waiting for Revit command '{command}' after {_timeoutMs}ms");
        }
    }

    public void Dispose() { }

    private static JToken? ToJToken(object? parameters)
    {
        if (parameters is null)
            return null;

        if (parameters is JToken token)
            return token;

        var json = System.Text.Json.JsonSerializer.Serialize(parameters);
        return JToken.Parse(json);
    }
}

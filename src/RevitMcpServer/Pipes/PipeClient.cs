using System.IO.Pipes;
using Newtonsoft.Json;
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
            Params = parameters != null
                ? Newtonsoft.Json.Linq.JToken.FromObject(parameters)
                : null,
            Id = $"{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}{Random.Shared.Next(100000, 999999)}"
        };

        var requestJson = JsonConvert.SerializeObject(request);
        await PipeProtocol.WriteMessageAsync(pipe, requestJson, ct);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(_timeoutMs);

        var responseJson = await PipeProtocol.ReadMessageAsync(pipe, timeoutCts.Token)
            ?? throw new TimeoutException($"No response from Revit for command '{command}'");

        var errorResponse = JsonConvert.DeserializeObject<JsonRpcErrorResponse>(responseJson);
        if (errorResponse?.Error?.Message != null && errorResponse.Error.Code != 0)
            throw new Exception($"Revit error ({errorResponse.Error.Code}): {errorResponse.Error.Message}");

        return responseJson;
    }

    public void Dispose() { }
}

using System.IO;
using System.IO.Pipes;
using System.Text;
using Newtonsoft.Json;
using RevitMcpSdk.Models;

namespace RevitMcpPlugin.Core;

public class PipeService
{
    public const string PipeName = "revit-mcp";
    private const int MaxMessageLength = 50_000_000;

    private readonly CommandExecutor _executor;
    private readonly Action<string>? _log;
    private volatile bool _running;
    private Thread? _listenerThread;

    public PipeService(ICommandRegistry registry, CommandExecutor executor, Action<string>? log = null)
    {
        _executor = executor;
        _log = log;
    }

    public bool IsRunning => _running;

    public void Start()
    {
        if (_running)
            return;

        _running = true;
        _listenerThread = new Thread(ListenLoop)
        {
            IsBackground = true,
            Name = "MCP-PipeListener"
        };
        _listenerThread.Start();
        Log($"Pipe service started on '{PipeName}'.");
    }

    public void Stop()
    {
        if (!_running)
            return;

        _running = false;
        try
        {
            using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut);
            client.Connect(100);
        }
        catch
        {
            // Best-effort wakeup if listener is blocked in WaitForConnection.
        }

        Log("Pipe service stopped.");
    }

    private void ListenLoop()
    {
        while (_running)
        {
            try
            {
                using var pipe = new NamedPipeServerStream(
                    PipeName,
                    PipeDirection.InOut,
                    NamedPipeServerStream.MaxAllowedServerInstances,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous,
                    inBufferSize: 1_048_576,
                    outBufferSize: 1_048_576);

                pipe.WaitForConnection();
                if (_running)
                {
                    Log("Client connected.");
                    HandleClient(pipe);
                }
            }
            catch (Exception) when (!_running)
            {
                break;
            }
            catch (Exception ex)
            {
                Log($"Error: {ex.Message}");
                Thread.Sleep(200);
            }
        }
    }

    private void HandleClient(NamedPipeServerStream pipe)
    {
        try
        {
            var requestJson = ReadLengthPrefixed(pipe);
            if (requestJson == null)
                return;

            var request = JsonConvert.DeserializeObject<JsonRpcRequest>(requestJson);
            var responseJson = request == null
                ? CreateErrorResponse("", -32600, "Invalid JSON-RPC request.")
                : _executor.Execute(request);
            if (request != null)
                Log($"Handled request '{request.Method}'.");

            WriteLengthPrefixed(pipe, responseJson);
        }
        catch (Exception ex)
        {
            try
            {
                WriteLengthPrefixed(pipe, CreateErrorResponse("", -32603, ex.Message));
            }
            catch
            {
                // Pipe may already be broken.
            }

            Log($"Client handling error: {ex.Message}");
        }
    }

    private static string? ReadLengthPrefixed(Stream stream)
    {
        var lengthBuf = new byte[4];
        var read = ReadExact(stream, lengthBuf, 4);
        if (read == 0)
            return null;
        if (read < 4)
            throw new InvalidDataException("Incomplete pipe frame length prefix.");

        var len = BitConverter.ToInt32(lengthBuf, 0);
        if (len <= 0 || len > MaxMessageLength)
            throw new InvalidDataException($"Invalid pipe frame length: {len}.");

        var msgBuf = new byte[len];
        read = ReadExact(stream, msgBuf, len);
        if (read < len)
            throw new InvalidDataException("Incomplete pipe frame payload.");

        return Encoding.UTF8.GetString(msgBuf);
    }

    private static void WriteLengthPrefixed(Stream stream, string message)
    {
        var bytes = Encoding.UTF8.GetBytes(message);
        if (bytes.Length <= 0 || bytes.Length > MaxMessageLength)
            throw new InvalidDataException($"Invalid pipe frame length: {bytes.Length}.");

        var prefix = BitConverter.GetBytes(bytes.Length);
        stream.Write(prefix, 0, 4);
        stream.Write(bytes, 0, bytes.Length);
        stream.Flush();
    }

    private static int ReadExact(Stream stream, byte[] buffer, int count)
    {
        var total = 0;
        while (total < count)
        {
            var n = stream.Read(buffer, total, count - total);
            if (n == 0)
                break;
            total += n;
        }

        return total;
    }

    private static string CreateErrorResponse(string id, int code, string message)
    {
        return JsonConvert.SerializeObject(new JsonRpcErrorResponse
        {
            Id = id,
            Error = new JsonRpcError { Code = code, Message = message }
        });
    }

    private void Log(string message)
    {
        System.Diagnostics.Debug.WriteLine($"[PipeService] {message}");
        _log?.Invoke($"[PipeService] {message}");
    }
}

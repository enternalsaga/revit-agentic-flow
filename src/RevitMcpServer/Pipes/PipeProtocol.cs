using System.Text;

namespace RevitMcpServer.Pipes;

public static class PipeProtocol
{
    private const int MaxMessageLength = 50_000_000;

    public static async Task WriteMessageAsync(Stream stream, string message, CancellationToken ct = default)
    {
        var bytes = Encoding.UTF8.GetBytes(message);
        if (bytes.Length <= 0 || bytes.Length > MaxMessageLength)
            throw new InvalidOperationException($"Pipe message length must be between 1 and {MaxMessageLength} bytes.");

        var lengthPrefix = BitConverter.GetBytes(bytes.Length);
        await stream.WriteAsync(lengthPrefix, 0, 4, ct);
        await stream.WriteAsync(bytes, 0, bytes.Length, ct);
        await stream.FlushAsync(ct);
    }

    public static async Task<string?> ReadMessageAsync(Stream stream, CancellationToken ct = default)
    {
        var lengthBuffer = new byte[4];
        var bytesRead = await ReadExactAsync(stream, lengthBuffer, 4, ct);
        if (bytesRead == 0) return null;
        if (bytesRead < 4)
            throw new InvalidDataException($"Incomplete pipe message length prefix: expected 4 bytes, read {bytesRead} bytes.");

        var messageLength = BitConverter.ToInt32(lengthBuffer, 0);
        if (messageLength <= 0 || messageLength > MaxMessageLength)
            throw new InvalidDataException($"Invalid pipe message length: {messageLength}.");

        var messageBuffer = new byte[messageLength];
        bytesRead = await ReadExactAsync(stream, messageBuffer, messageLength, ct);
        if (bytesRead < messageLength)
            throw new InvalidDataException($"Incomplete pipe message payload: expected {messageLength} bytes, read {bytesRead} bytes.");

        return Encoding.UTF8.GetString(messageBuffer);
    }

    private static async Task<int> ReadExactAsync(Stream stream, byte[] buffer, int count, CancellationToken ct)
    {
        int totalRead = 0;
        while (totalRead < count)
        {
            int read = await stream.ReadAsync(buffer, totalRead, count - totalRead, ct);
            if (read == 0) break;
            totalRead += read;
        }

        return totalRead;
    }
}

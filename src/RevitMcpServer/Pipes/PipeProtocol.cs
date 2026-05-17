using System.Text;

namespace RevitMcpServer.Pipes;

public static class PipeProtocol
{
    public static async Task WriteMessageAsync(Stream stream, string message, CancellationToken ct = default)
    {
        var bytes = Encoding.UTF8.GetBytes(message);
        var lengthPrefix = BitConverter.GetBytes(bytes.Length);
        await stream.WriteAsync(lengthPrefix, 0, 4, ct);
        await stream.WriteAsync(bytes, 0, bytes.Length, ct);
        await stream.FlushAsync(ct);
    }

    public static async Task<string?> ReadMessageAsync(Stream stream, CancellationToken ct = default)
    {
        var lengthBuffer = new byte[4];
        var bytesRead = await ReadExactAsync(stream, lengthBuffer, 4, ct);
        if (bytesRead < 4) return null;

        var messageLength = BitConverter.ToInt32(lengthBuffer, 0);
        if (messageLength <= 0 || messageLength > 50_000_000) return null;

        var messageBuffer = new byte[messageLength];
        bytesRead = await ReadExactAsync(stream, messageBuffer, messageLength, ct);
        if (bytesRead < messageLength) return null;

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

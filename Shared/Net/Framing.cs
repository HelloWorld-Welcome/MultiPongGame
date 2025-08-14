using System.Buffers.Binary;
using System.Text;
using System.Text.Json;

namespace Shared.Net
{
    public static class Framing
    {
        static readonly JsonSerializerOptions JsonOpts = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            WriteIndented = false
        };

        public static async Task WriteAsync<T>(
            Stream stream, OpCode type, T payload, CancellationToken ct)
        {
            byte[] json = JsonSerializer.SerializeToUtf8Bytes(payload, JsonOpts);

            // Span/stackalloc 대신 byte[] 사용
            byte[] header = new byte[5];
            int len = json.Length;
            header[0] = (byte)((len >> 24) & 0xFF);
            header[1] = (byte)((len >> 16) & 0xFF);
            header[2] = (byte)((len >> 8) & 0xFF);
            header[3] = (byte)(len & 0xFF);
            header[4] = (byte)type;

            // .NET 버전 호환을 위해 offset/length 오버로드 사용
            await stream.WriteAsync(header, 0, header.Length, ct);
            await stream.WriteAsync(json, 0, json.Length, ct);
            await stream.FlushAsync(ct);
        }

        public static async Task<(OpCode type, byte[] json)> ReadAsync(
            Stream stream, CancellationToken ct)
        {
            // read header
            byte[] header = await ReadExactAsync(stream, 5, ct);
            int len = BinaryPrimitives.ReadInt32BigEndian(header);
            var type = (OpCode)header[4];

            // read body
            byte[] body = await ReadExactAsync(stream, len, ct);
            return (type, body);
        }

        public static async Task<byte[]> ReadExactAsync(
            Stream s, int size, CancellationToken ct)
        {
            byte[] buf = new byte[size];
            int off = 0;
            while (off < size)
            {
                int n = await s.ReadAsync(buf.AsMemory(off, size - off), ct);
                if (n == 0) throw new IOException("Remote closed");
                off += n;
            }
            return buf;
        }

        public static T Deserialize<T>(byte[] json)
            => JsonSerializer.Deserialize<T>(json, JsonOpts)
               ?? throw new InvalidOperationException("JSON -> DTO fail");
    }

}

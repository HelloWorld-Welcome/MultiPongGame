using System.Buffers.Binary;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading.Channels;
using Shared.Net;
using Shared;

namespace Server
{
    public sealed class ClientSession
    {
        readonly TcpClient _client;
        readonly NetworkStream _stream;
        readonly CancellationToken _ct;
        readonly Channel<(OpCode, byte[])> _outbound = Channel.CreateUnbounded<(OpCode, byte[])>();

        public DateTime CreatedAt { get; } = DateTime.UtcNow;
        public int PlayerNumber { get; set; } = 0; // 1 or 2, 0=spectator
        public InputState LatestInput { get; private set; } = new(false, false);

        public event Action<ClientSession>? Closed;

        public ClientSession(TcpClient client, CancellationToken ct)
        {
            _client = client;
            _stream = client.GetStream();
            _ct = ct;
            _ = WriterLoop(); // fire & forget
        }

        // 세션 자신을 함께 넘기도록
        public async Task ReaderLoopAsync(Func<ClientSession, OpCode, byte[], Task> dispatcher)
        {
            try
            {
                while (!_ct.IsCancellationRequested)
                {
                    var (type, json) = await Framing.ReadAsync(_stream, _ct);
                    await dispatcher(this, type, json);                 // ★ this 추가
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERR] ReaderLoop: {ex.Message}");
            }
            finally
            {
                try { _client.Close(); } catch { }
                Closed?.Invoke(this);
            }
        }

        public void SetInput(InputState s) => LatestInput = s;

        public ValueTask SendAsync<T>(OpCode type, T payload)
            => EnqueueAsync(type, JsonSerializer.SerializeToUtf8Bytes(payload));

        async ValueTask EnqueueAsync(OpCode t, byte[] body)
        {
            await _outbound.Writer.WriteAsync((t, body), _ct);
        }

        async Task WriterLoop()
        {
            try
            {
                while (await _outbound.Reader.WaitToReadAsync(_ct))
                    while (_outbound.Reader.TryRead(out var item))
                    {
                        var body = item.Item2; // 이미 JSON 바이트
                        int len = body.Length;

                        // Span/stackalloc 제거
                        byte[] header = new byte[5];
                        header[0] = (byte)((len >> 24) & 0xFF);
                        header[1] = (byte)((len >> 16) & 0xFF);
                        header[2] = (byte)((len >> 8) & 0xFF);
                        header[3] = (byte)(len & 0xFF);
                        header[4] = (byte)item.Item1;

                        await _stream.WriteAsync(header, 0, header.Length, _ct);
                        await _stream.WriteAsync(body, 0, body.Length, _ct);
                        await _stream.FlushAsync(_ct);
                    }
            }
            catch (Exception)
            {
                // log
            }
        }


        public void Close() { try { _client.Close(); } catch { } }
    }
}

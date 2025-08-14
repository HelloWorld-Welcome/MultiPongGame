using Shared;
using Shared.Net;
using System.Net.Sockets;

namespace Client
{   
    public sealed class GameClient : IDisposable
    {
        readonly TcpClient _tcp = new();
        NetworkStream _stream = default!;
        CancellationTokenSource _cts = new();

        public event Action<UpdateSnapshot>? OnSnapshot;
        public event Action<int>? OnRoleChanged;
        public int myRole = 0;

        public async Task ConnectAsync(string host, int port, string name, CancellationToken ct)
        {
            await _tcp.ConnectAsync(host, port, ct);
            _stream = _tcp.GetStream();

            // 입장
            await Framing.WriteAsync(_stream, OpCode.EnterRequest, new EnterRequest(name), ct);

            // 수신 루프
            _ = Task.Run(ReceiveLoop);
        }

        async Task ReceiveLoop()
        {
            try
            {
                while (!_cts.IsCancellationRequested)
                {
                    var (type, body) = await Framing.ReadAsync(_stream, _cts.Token);
                    if (type == OpCode.UpdateSnapshot)
                        OnSnapshot?.Invoke(Framing.Deserialize<UpdateSnapshot>(body));
                    else if (type == OpCode.EnterResponse)
                    {
                        var er = Framing.Deserialize<EnterResponse>(body);
                        myRole = er.PlayerNumber;
                        OnRoleChanged?.Invoke(myRole);
                    }
                    else if (type == OpCode.LeaveResponse) break;
                }
            }
            catch { /* log */ }
        }

        public Task SendInputAsync(bool up, bool down, CancellationToken ct = default)
        {
            return Framing.WriteAsync(_stream, OpCode.InputState, new InputState(up, down), ct);
        }

        public Task LeaveAsync(CancellationToken ct = default)
            => Framing.WriteAsync(_stream, OpCode.LeaveRequest, new LeaveRequest(), ct);

        public void Dispose()
        {
            _cts.Cancel();
            try { _tcp.Close(); } catch { }
            _cts.Dispose();
        }
    }
}

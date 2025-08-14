using Shared.Net;
using Shared;
using System.Diagnostics;
using System.Net.Sockets;
using System.Net;

namespace Server
{
    public sealed class Server
    {
        readonly TcpListener _listener;
        readonly CancellationTokenSource _cts = new();
        readonly List<ClientSession> _clients = new();
        readonly GameLoop _game = new();

        public Server(int port) => _listener = new TcpListener(IPEndPoint.Parse($"127.0.0.1:{port}"));

        public async Task RunAsync()
        {
            _listener.Start();
            _ = Task.Run(TickLoop);

            while (!_cts.IsCancellationRequested)
            {
                var tcp = await _listener.AcceptTcpClientAsync(_cts.Token);
                var session = new ClientSession(tcp, _cts.Token);

                // 끊김 시 정리
                session.Closed += OnSessionClosed;

                lock (_clients) _clients.Add(session);

                // 자리 재배치 (접속 시마다)
                ReassignPlayersLocked();

                // 플레이어 번호 할당 (최초 2명)
                //session.PlayerNumber = PickPlayerNumber();

                // 환영/역할 통지
                //await session.SendAsync(OpCode.EnterResponse, new EnterResponse(session.PlayerNumber));

                // 수신 루프
                _ = session.ReaderLoopAsync(async (sess, op, json) =>
                {
                    if (op == OpCode.InputState)
                    {
                        var input = Framing.Deserialize<InputState>(json);
                        // 관전자라도 일단 저장 (틱에서 P1/P2만 사용)
                        sess.SetInput(input);

                        // 클라이언트 키입력 로그 (여기서 P1과 P2가 찍혀야 정상)
                        //Console.WriteLine($"[IN ] P{sess.PlayerNumber}: up={input.Up}, down={input.Down}");
                    }
                    else if (op == OpCode.LeaveRequest)
                    {
                        await sess.SendAsync(OpCode.LeaveResponse, new LeaveResponse());
                        sess.Close();
                    }
                });
            }
        }

        void OnSessionClosed(ClientSession s)
        {
            s.Closed -= OnSessionClosed;
            lock (_clients) _clients.Remove(s);
            ReassignPlayersLocked();     // ★ 종료 때마다
        }
        void EvaluateGameStateLocked()
        {
            // 반드시 lock(_clients) 안에서 호출
            bool hasP1 = _clients.Any(c => c.PlayerNumber == 1);
            bool hasP2 = _clients.Any(c => c.PlayerNumber == 2);

            if (hasP1 && hasP2) _game.StartGame();
            else _game.PauseGame();
        }

        void ReassignPlayersLocked()
        {
            lock (_clients)
            {
                // 1) 일괄 초기화
                foreach (var s in _clients) s.PlayerNumber = 0;

                // 2) CreatedAt 오래된 순으로 1,2 부여
                int next = 1;
                foreach (var s in _clients.OrderBy(s => s.CreatedAt))
                    s.PlayerNumber = (next <= 2) ? next++ : 0;

                // 3) 모든 클라이언트에 현재 역할 통지 (여기만!)
                foreach (var s in _clients)
                {
                    _ = s.SendAsync(OpCode.EnterResponse, new EnterResponse(s.PlayerNumber));
                    Console.WriteLine($"[ROLE] -> {s.CreatedAt:HH:mm:ss} = P{s.PlayerNumber}");
                }

                Console.WriteLine("[SEAT] " + string.Join(", ",
                    _clients.Select(c => $"{c.CreatedAt:HH:mm:ss}=P{c.PlayerNumber}")));

                // ★ 좌석 확정 후 "딱 한 번" 게임 상태 평가
                EvaluateGameStateLocked();
            }
        }
        async Task TickLoop()
        {
            var sw = new Stopwatch();
            const double dtMs = 1000.0 / 60.0;
            sw.Start();
            double acc = 0;
            long last = sw.ElapsedMilliseconds;

            while (!_cts.IsCancellationRequested)
            {
                long now = sw.ElapsedMilliseconds;
                acc += (now - last);
                last = now;

                while (acc >= dtMs)
                {
                    ClientSession? c1, c2;
                    lock (_clients)
                    {
                        c1 = _clients.FirstOrDefault(c => c.PlayerNumber == 1);
                        c2 = _clients.FirstOrDefault(c => c.PlayerNumber == 2);
                    }

                    // ★ 입력은 항상 최신으로 읽어 둔다
                    var p1 = c1?.LatestInput ?? new InputState(false, false);
                    var p2 = c2?.LatestInput ?? new InputState(false, false);

                    // ★ 물리 진행 여부만 GameLoop가 결정
                    _game.Tick(p1, p2);

                    var snap = _game.Snapshot();

                    List<ClientSession> copy;
                    lock (_clients) copy = _clients.ToList();
                    foreach (var c in copy)
                        _ = c.SendAsync(OpCode.UpdateSnapshot, snap);

                    acc -= dtMs;
                }

                await Task.Delay(1);
            }
        }

        public void Stop() => _cts.Cancel();
    }
}

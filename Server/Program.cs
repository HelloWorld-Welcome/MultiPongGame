using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Server
{
    internal static class Program
    {
        static async Task<int> Main(string[] args)
        {
            // 포트 인자 파싱 (기본 7777)
            int port = 7777;
            if (args.Length > 0 && int.TryParse(args[0], out var p)) port = p;

            var server = new Server(port);
            Console.WriteLine($"[Server] Listening on 127.0.0.1:{port}");
            Console.WriteLine("Press Ctrl+C to stop.");

            // Ctrl+C 취소 대기
            var exitTcs = new TaskCompletionSource();
            Console.CancelKeyPress += (s, e) =>
            {
                e.Cancel = true;          // 프로세스 즉시 종료 막고 우리가 정리
                exitTcs.TrySetResult();
            };

            // 서버 루프 시작
            var serverTask = server.RunAsync();

            // Ctrl+C 들어오면 종료
            await exitTcs.Task;
            Console.WriteLine("[Server] Stopping...");
            server.Stop();

            try { await serverTask; }    // 정상 종료 대기
            catch (Exception ex)
            {
                Console.WriteLine($"[Server] Stopped with error: {ex.Message}");
            }

            Console.WriteLine("[Server] Bye.");
            return 0;
        }
    }
}

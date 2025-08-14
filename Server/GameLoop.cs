using Shared;
using System.Drawing;

namespace Server
{
    public enum MatchPhase { Lobby, Playing, Paused }
    public sealed class GameLoop
    {
        public MatchPhase Phase { get; private set; } = MatchPhase.Lobby;
     
        readonly object _lock = new();
        // 상태
        public Point P1, P2, Ball;
        public int Score1, Score2;
        public bool WithP1 = true, WithP2 = true, WithBall = true;
        public bool IsRunning => Phase == MatchPhase.Playing;

        Size Map = new(800, 600);
        Size Stick = new(10, 80);
        Size BallSize = new(10, 10);
        Point BallVel = new(4, 3);

        public GameLoop()
        {
            // 패들 세로 중앙
            P1 = new Point(P1.X, Map.Height / 2 - Stick.Height / 2);
            P2 = new Point(P2.X, Map.Height / 2 - Stick.Height / 2);

            ResetBall(dirRight: true);
        }

        public void StartGame()
        {
            if (Phase == MatchPhase.Playing) return;
            Phase = MatchPhase.Playing;
            if ((BallVel.X == 0 && BallVel.Y == 0) || (Ball.X == 0 && Ball.Y == 0))
                ResetBall(dirRight: true); // 킥오프 위치/속도 보장
        }

        public void PauseGame()
        {
            if (Phase == MatchPhase.Paused) return;
            Phase = MatchPhase.Paused;
            // 공을 화면 중앙에 멈춰둠(보이게나 숨기게는 취향대로)
            Ball = new(Map.Width / 2 - BallSize.Width / 2, Map.Height / 2 - BallSize.Height / 2);
            BallVel = new(0, 0);
        }

        public void StopToLobby()
        {
            Phase = MatchPhase.Lobby;
            Score1 = Score2 = 0;
            Ball = new(Map.Width / 2 - BallSize.Width / 2, Map.Height / 2 - BallSize.Height / 2);
            BallVel = new(0, 0);
        }

        public void Tick(InputState p1In, InputState p2In)
        {
            if (!IsRunning) return;

            lock (_lock)
            {
                // 패들 이동
                if (p1In.Up) P1.Y -= 6;
                if (p1In.Down) P1.Y += 6;
                if (p2In.Up) P2.Y -= 6;
                if (p2In.Down) P2.Y += 6;
                P1.Y = Math.Clamp(P1.Y, 0, Map.Height - Stick.Height);
                P2.Y = Math.Clamp(P2.Y, 0, Map.Height - Stick.Height);

                // 공 이동
                Ball.Offset(BallVel);

                // 상하벽 반사
                if (Ball.Y <= 0 || Ball.Y + BallSize.Height >= Map.Height) BallVel.Y = -BallVel.Y;

                // 패들 충돌(단순 AABB)
                var ballRect = new Rectangle(Ball, BallSize);
                var p1Rect = new Rectangle(new Point(20, P1.Y), Stick);
                var p2Rect = new Rectangle(new Point(Map.Width - 20 - Stick.Width, P2.Y), Stick);
                if (ballRect.IntersectsWith(p1Rect)) BallVel.X = Math.Abs(BallVel.X);
                if (ballRect.IntersectsWith(p2Rect)) BallVel.X = -Math.Abs(BallVel.X);

                // 득점
                if (Ball.X < -BallSize.Width) { Score2++; ResetBall(dirRight: true); }
                if (Ball.X > Map.Width) { Score1++; ResetBall(dirRight: false); }
            }
        }

        void ResetBall(bool dirRight)
        {
            Ball = new(Map.Width / 2 - BallSize.Width / 2,
                       Map.Height / 2 - BallSize.Height / 2);
            BallVel = new(dirRight ? 4 : -4, 3);
        }

        public UpdateSnapshot Snapshot()
        {
            lock (_lock)
            {
                return new UpdateSnapshot(
                    20, P1.Y, Map.Width - 20 - Stick.Width, P2.Y,
                    Ball.X, Ball.Y, Score1, Score2,
                    WithP1, WithP2, WithBall
                );
            }
        }
    }
}

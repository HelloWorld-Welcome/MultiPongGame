namespace Shared
{
    public sealed record EnterRequest(string Name);
    public sealed record EnterResponse(int PlayerNumber);

    public sealed record LeaveRequest();
    public sealed record LeaveResponse();

    public sealed record InputState(bool Up, bool Down);

    public sealed record UpdateSnapshot(
        int P1X, int P1Y,
        int P2X, int P2Y,
        int BallX, int BallY,
        int Score1, int Score2,
        bool WithP1, bool WithP2, bool WithBall
    );
}

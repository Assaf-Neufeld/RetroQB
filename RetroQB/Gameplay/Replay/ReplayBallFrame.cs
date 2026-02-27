using System.Numerics;
using RetroQB.Entities;

namespace RetroQB.Gameplay.Replay;

public readonly record struct ReplayBallFrame(
    Vector2 Position,
    Vector2 Velocity,
    BallState State,
    int HolderId,
    float AirTime,
    Vector2 ThrowStart,
    float IntendedDistance,
    float ArcApexHeight);

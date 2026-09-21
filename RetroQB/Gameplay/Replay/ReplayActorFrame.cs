using System.Numerics;
using Raylib_cs;
using RetroQB.Entities;

namespace RetroQB.Gameplay.Replay;

public enum ReplayActorKind
{
    Quarterback,
    Receiver,
    Blocker,
    Defender
}

public readonly record struct ReplayActorFrame(
    int Id,
    ReplayActorKind Kind,
    Vector2 Position,
    Vector2 Velocity,
    float Radius,
    string Glyph,
    Color Color,
    bool HasBall,
    bool Eligible,
    bool IsBlocking,
    PlayerVisualFrame Visual = default,
    bool IsStarPlayer = false);

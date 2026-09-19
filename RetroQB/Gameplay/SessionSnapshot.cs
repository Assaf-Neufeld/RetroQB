using System.Numerics;
using RetroQB.AI;
using RetroQB.Entities;

namespace RetroQB.Gameplay;

internal sealed record ActorSnapshot(string Label, Vector2 Position, Vector2 Velocity);

/// <summary>Detached observations for verification. No entity or mutable rules objects escape.</summary>
internal sealed record SessionSnapshot(
    GameState State, bool Paused, string PlayId, string Offense, string Defense,
    int Down, float Distance, float LineOfScrimmage, float FirstDownLine, int Score, int AwayScore,
    BallState BallState, Vector2 BallPosition, string? Carrier, CoverageScheme Coverage, string[] Blitzers,
    string Exchange, KickPhase? KickPhase, float? KickMarker, PlayOutcome? Outcome, float? Gain,
    string Result, GameStatsSnapshot Stats, int ReplayFrameCount, ActorSnapshot[] Actors);

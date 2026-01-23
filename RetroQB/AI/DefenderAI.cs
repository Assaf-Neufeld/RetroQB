using System.Numerics;
using RetroQB.Entities;

namespace RetroQB.AI;

/// <summary>
/// Facade for defender AI functionality. Delegates to specialized classes:
/// - DefenderTargeting: Target selection based on game state
/// - ZoneCoverage: Zone coverage logic including bounds and receiver matching
/// </summary>
public static class DefenderAI
{
    /// <summary>
    /// Updates a defender's position and velocity based on current game state.
    /// </summary>
    public static void UpdateDefender(
        Defender defender,
        Quarterback qb,
        IReadOnlyList<Receiver> receivers,
        Ball ball,
        float speedMultiplier,
        float dt,
        bool qbIsRunner,
        bool useZoneCoverage,
        float lineOfScrimmage)
    {
        float speed = defender.Speed * speedMultiplier;

        Vector2 target = DefenderTargeting.GetTarget(
            defender,
            qb,
            receivers,
            ball,
            qbIsRunner,
            useZoneCoverage,
            lineOfScrimmage);

        MoveTowardsTarget(defender, target, speed, dt);
    }

    private static void MoveTowardsTarget(Defender defender, Vector2 target, float speed, float dt)
    {
        Vector2 dir = target - defender.Position;
        if (dir.LengthSquared() > 0.001f)
        {
            dir = Vector2.Normalize(dir);
        }
        defender.Velocity = dir * speed;
        defender.Position += defender.Velocity * dt;
    }
}

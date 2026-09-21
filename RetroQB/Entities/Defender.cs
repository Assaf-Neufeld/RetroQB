using System.Numerics;
using Raylib_cs;
using RetroQB.Core;
using RetroQB.Data;

namespace RetroQB.Entities;

public enum CoverageRole
{
    None,
    DeepLeft,
    DeepMiddle,
    DeepRight,
    DeepQuarterLeft,
    DeepQuarterRight,
    FlatLeft,
    FlatRight,
    HookLeft,
    HookMiddle,
    HookRight,
    Robber
}

public enum DefensivePosition
{
    DL,
    DE,
    LB,
    DB
}

public sealed class Defender : Entity
{
    public Vector2 AlignmentPosition { get; private set; }
    public DefensivePosition PositionRole { get; }
    public DefenderSlot Slot { get; }
    private readonly float _baseSpeed;
    public float Speed => _baseSpeed * SpeedMultiplier;
    public float SpeedMultiplier { get; private set; } = 1f;
    public float InterceptionMultiplier { get; private set; } = 1f;
    public float TackleMultiplier { get; private set; } = 1f;
    public float BlockShedMultiplier { get; private set; } = 1f;
    public bool IsRusher { get; set; }
    public int CoverageReceiverIndex { get; set; } = -1;
    public int MatchReceiverIndex { get; set; } = -1;
    public bool HasBall { get; set; }
    public CoverageRole ZoneRole { get; set; } = CoverageRole.None;
    public float RushLaneOffsetX { get; set; }
    
    /// <summary>
    /// Reference to the team attributes for this defender.
    /// </summary>
    public DefensiveTeamAttributes TeamAttributes { get; }
    
    /// <summary>
    /// If true, DB plays press coverage close to the line of scrimmage.
    /// If false, DB plays off coverage and maintains cushion before pursuing.
    /// </summary>
    public bool IsPressCoverage { get; set; }

    /// <summary>
    /// Per-play random horizontal offset applied to zone anchor positions.
    /// Shifts zone coverage seams to create play-to-play variety.
    /// </summary>
    public float ZoneJitterX { get; set; }

    /// <summary>
    /// Set each frame by blocking logic. True when a blocker has active contact with this defender.
    /// Reset at the start of each blocking update cycle.
    /// </summary>
    public bool IsBeingBlocked { get; set; }

    /// <summary>
    /// Number of active blockers currently in contact this frame.
    /// Reset at the start of each blocking update cycle.
    /// </summary>
    public int ActiveBlockersCount { get; set; }

    public Defender(Vector2 position, DefensivePosition role, DefenderSlot slot, DefensiveTeamAttributes? teamAttributes = null)
        : base(position, Constants.DefenderRadius, role.ToString(), teamAttributes?.PrimaryColor ?? Palette.Red)
    {
        AlignmentPosition = position;
        TeamAttributes = teamAttributes ?? DefensiveTeamAttributes.Default;
        PositionRole = role;
        Slot = slot;
        _baseSpeed = TeamAttributes.GetEffectiveSpeed(role);
        var profile = TeamAttributes.Roster.GetProfile(slot);
        SpeedMultiplier = Math.Clamp(profile.SpeedRating, .7f, 1.4f);
        TackleMultiplier = Math.Clamp(profile.TackleRating, .7f, 1.4f);
        InterceptionMultiplier = Math.Clamp(profile.CoverageRating, .7f, 1.4f);
        BlockShedMultiplier = Math.Clamp(profile.BlockShedRating, .7f, 1.4f);
        IsStarPlayer = profile.IsStarPlayer;
    }

    public void ApplyStarBoost(float speedMultiplier, float tackleMultiplier, float interceptionMultiplier, float blockShedMultiplier)
    {
        IsStarPlayer = true;
        SpeedMultiplier *= speedMultiplier;
        TackleMultiplier *= tackleMultiplier;
        InterceptionMultiplier *= interceptionMultiplier;
        BlockShedMultiplier *= blockShedMultiplier;
    }

    internal void RestoreModifiers(float speed, float tackle, float interception, float shed, bool star)
    {
        SpeedMultiplier = speed;
        TackleMultiplier = tackle;
        InterceptionMultiplier = interception;
        BlockShedMultiplier = shed;
        IsStarPlayer = star;
    }

    internal void SetAlignment(Vector2 position)
    {
        AlignmentPosition = position;
        Position = position;
    }
}

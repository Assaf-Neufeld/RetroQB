using System.Numerics;
using System.Text.Json.Serialization;
using RetroQB.Entities;

namespace RetroQB.Gameplay.Defense;

public readonly record struct ReceiverId(int Value);
public sealed record VisibleSkillPlayer(ReceiverId Id, ReceiverSlot Slot, Vector2 Position);
public sealed record VisibleLineman(int Id, Vector2 Position);

/// <summary>Copies only pre-snap visible facts. No routes, eligibility, blocking jobs, ratings or offensive call ID.</summary>
public sealed class OffensiveAlignmentSnapshot
{
    public Vector2 Ball { get; }
    public Vector2 Quarterback { get; }
    public float LineOfScrimmage { get; }
    public IReadOnlyList<VisibleSkillPlayer> Skills { get; }
    public IReadOnlyList<VisibleLineman> Linemen { get; }
    public OffensiveAlignmentSnapshot(Vector2 ball, Vector2 quarterback, float lineOfScrimmage,
        IEnumerable<VisibleSkillPlayer> skills, IEnumerable<VisibleLineman> linemen)
    {
        Ball = ball; Quarterback = quarterback; LineOfScrimmage = lineOfScrimmage;
        Skills = Array.AsReadOnly(skills.OrderBy(p => p.Id.Value).ToArray());
        Linemen = Array.AsReadOnly(linemen.OrderBy(p => p.Id).ToArray());
        if (!InField(ball) || !InField(quarterback) || !float.IsFinite(lineOfScrimmage)
            || lineOfScrimmage < FieldGeometry.EndZoneDepth || lineOfScrimmage > FieldGeometry.OpponentGoalLine
            || Skills.Count is < 1 or > 5 || Linemen.Count is < 5 or > 9 || Skills.Count + Linemen.Count != 10
            || Skills.Any(p => p.Id.Value < 0 || !Enum.IsDefined(p.Slot) || !InField(p.Position))
            || Linemen.Any(p => p.Id < 0 || !InField(p.Position))
            || Skills.Select(p => p.Id).Distinct().Count() != Skills.Count
            || Skills.Select(p => p.Slot).Distinct().Count() != Skills.Count
            || Linemen.Select(p => p.Id).Distinct().Count() != Linemen.Count)
            throw new ArgumentException("Alignment needs eleven distinct offensive actors with finite legal visible geometry.");
    }

    public static OffensiveAlignmentSnapshot From(FormationResult formation, float los) => new(
        formation.Ball.Position, formation.Qb.Position, los,
        formation.Receivers.Select(r => new VisibleSkillPlayer(new(r.Index), r.Slot, r.Position)),
        // Blocker entities have no persistent IDs. This adapter assigns pre-snap left-to-right IDs;
        // custom snapshots may supply roster IDs. Neither identity is used as a list subscript.
        formation.Blockers.OrderBy(b => b.Position.X).ThenBy(b => b.Position.Y)
            .Select((b, i) => new VisibleLineman(i, b.Position)));

    public bool SameGeometry(OffensiveAlignmentSnapshot other) => Ball == other.Ball && Quarterback == other.Quarterback
        && LineOfScrimmage == other.LineOfScrimmage && Skills.SequenceEqual(other.Skills) && Linemen.SequenceEqual(other.Linemen);
    internal static bool InField(Vector2 p) => float.IsFinite(p.X) && float.IsFinite(p.Y)
        && p.X >= 0 && p.X <= Constants.FieldWidth && p.Y >= 0 && p.Y <= Constants.FieldLength;
}

public enum DefensePackage { Base, Nickel }
public enum ManShell { Zero, SingleHigh, TwoHigh, Robber }
public enum FieldSide { Left = -1, Balanced = 0, Right = 1 }
public enum HelperRole { DeepMiddle, DeepLeft, DeepRight, LowMiddle, Robber, Spy }
public enum RunRole { Gap, Force, Cutback, Pursuit, DeepSupport }
public enum RunTrigger { ObservedRunCommitment }
public enum ContainRole { None, LeftEdge, RightEdge }
public sealed record ConditionalRunResponsibility(RunRole Role, FieldSide Side, RunTrigger Trigger, int? GapIndex = null);

// A discriminated union prevents contradictory rush/man/zone flags in compiled plans.
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(ManResponsibility), "man")]
[JsonDerivedType(typeof(HelpResponsibility), "help")]
[JsonDerivedType(typeof(RushResponsibility), "rush")]
[JsonDerivedType(typeof(ZoneResponsibility), "zone")]
[JsonDerivedType(typeof(MatchResponsibility), "match")]
public abstract record PassResponsibility;
public sealed record ManResponsibility(ReceiverId Target) : PassResponsibility;
public sealed record HelpResponsibility(HelperRole Role, ReceiverId? BracketTarget = null) : PassResponsibility;
public sealed record RushResponsibility(int GapIndex, ContainRole Contain) : PassResponsibility;
public sealed record DefensiveAssignment(DefenderSlot ActorId, DefensivePosition PositionRole, Vector2 Alignment,
    PassResponsibility Pass, ConditionalRunResponsibility Run);

/// <summary>Versioned supported budgets, with separate man and spot-zone catalogs.</summary>
public sealed record DefensiveCallDefinition(string Id, int Version, ManShell Shell, int RushCount, SpotZoneFamily Zone = SpotZoneFamily.None, MatchFamily Match = MatchFamily.None)
{
    public int RequiredDeepHelpers => Shell == ManShell.TwoHigh ? 2 : Shell == ManShell.Zero ? 0 : 1;
    public static IReadOnlyList<DefensiveCallDefinition> Supported { get; } = Array.AsReadOnly(new[]
    {
        new DefensiveCallDefinition("def.cover1", 1, ManShell.SingleHigh, 4),
        new DefensiveCallDefinition("def.cover2-man", 1, ManShell.TwoHigh, 4),
        new DefensiveCallDefinition("def.robber", 1, ManShell.Robber, 4),
        new DefensiveCallDefinition("def.edge", 1, ManShell.SingleHigh, 5),
        new DefensiveCallDefinition("def.zero", 1, ManShell.Zero, 6)
    });
    public static IReadOnlyList<DefensiveCallDefinition> SpotZones { get; } = Array.AsReadOnly(new[]
    {
        new DefensiveCallDefinition("def.cover2-zone", 1, ManShell.TwoHigh, 4, SpotZoneFamily.Cover2),
        new DefensiveCallDefinition("def.cover3", 1, ManShell.SingleHigh, 4, SpotZoneFamily.Cover3),
        new DefensiveCallDefinition("def.cover3-match", 1, ManShell.SingleHigh, 4, SpotZoneFamily.Buzz),
        new DefensiveCallDefinition("def.cover4", 1, ManShell.TwoHigh, 4, SpotZoneFamily.Cover4)
    });
    // v1 IDs remain historical spot-zone semantics. Match uses new IDs and never rewrites recordings.
    public static IReadOnlyList<DefensiveCallDefinition> Matches { get; } = Array.AsReadOnly(new[]
    {
        new DefensiveCallDefinition("def.cover3-match.v2", 2, ManShell.SingleHigh, 4, Match: MatchFamily.Cover3),
        new DefensiveCallDefinition("def.quarters-match.v2", 2, ManShell.TwoHigh, 4, Match: MatchFamily.Quarters)
    });
    public static IEnumerable<DefensiveCallDefinition> All => Supported.Concat(SpotZones).Concat(Matches);
    public static DefensiveCallDefinition Get(string id) => All.SingleOrDefault(c => c.Id == id)
        ?? throw new NotSupportedException($"'{id}' has no compiled plan; use the legacy engine until its phase is implemented.");
}

public sealed class ResolvedDefensivePlan
{
    public const string RulesVersion = "defense-plan-p7-v1";
    public string RuleVersion => RulesVersion;
    public DefensiveCallDefinition Call { get; }
    public DefensiveCallDefinition RequestedCall { get; }
    public string? FallbackReason { get; }
    public MatchCoveragePlan? Match { get; }
    public OffensiveAlignmentSnapshot Offense { get; }
    public OffensiveSurface Surface { get; }
    public DefensePackage Package { get; }
    public int ResolutionSeed { get; }
    public DefenderSlot ControlledActor => Package == DefensePackage.Base ? DefenderSlot.MLB : DefenderSlot.OLB1;
    public IReadOnlyList<DefensiveAssignment> Assignments { get; }
    public RunFitPlan Fits { get; }
    public PressurePlan Pressure { get; }
    public double PairingCost { get; }
    public ManCoverageAdjustments Adjustments { get; }
    public IReadOnlyList<BunchSwitchGroup> SwitchGroups { get; }
    public string TripsCheck => Match?.Check ?? "LockAllPrimaries";
    public string BunchCheck => SwitchGroups.Count == 0 ? "LockActorIds" : "SwitchPairsOnce;OtherGroupsLock";

    internal ResolvedDefensivePlan(DefensiveCallDefinition call, OffensiveAlignmentSnapshot offense,
        OffensiveSurface surface, DefensePackage package, int seed, IEnumerable<DefensiveAssignment> assignments, double pairingCost,
        ManCoverageAdjustments? adjustments = null, PressureSelection? pressure = null, MatchCoveragePlan? match = null,
        DefensiveCallDefinition? requestedCall = null, string? fallbackReason = null)
    {
        Call = call; Offense = offense; Surface = surface; Package = package; ResolutionSeed = seed;
        Match = match; RequestedCall = requestedCall ?? call; FallbackReason = fallbackReason;
        // Keep caller-supplied jobs intact so validation can detect corruption. Compilers bind fits before construction.
        Assignments = Array.AsReadOnly(assignments.OrderBy(a => a.ActorId).ToArray()); PairingCost = pairingCost;
        Pressure = PressureCompiler.Describe(offense, surface, Assignments, pressure ?? PressureSelection.For(call));
        Fits = new(Array.AsReadOnly(Assignments.Where(a => a.Run?.GapIndex != null)
            .Select(a => new GapFit(a.Run.GapIndex!.Value, a.ActorId)).OrderBy(g => g.GapIndex).ToArray()),
            Assignments.FirstOrDefault(a => a.Run is { Role: RunRole.Force, Side: FieldSide.Left })?.ActorId ?? DefenderSlot.DE1,
            Assignments.FirstOrDefault(a => a.Run is { Role: RunRole.Force, Side: FieldSide.Right })?.ActorId ?? DefenderSlot.DE2,
            Assignments.FirstOrDefault(a => a.Run is { Role: RunRole.Cutback, Side: FieldSide.Left })?.ActorId ?? DefenderSlot.DE1,
            Assignments.FirstOrDefault(a => a.Run is { Role: RunRole.Cutback, Side: FieldSide.Right })?.ActorId ?? DefenderSlot.DE2,
            Assignments.FirstOrDefault(a => a.Run?.Role == RunRole.DeepSupport)?.ActorId ?? DefenderSlot.DE1);
        Adjustments = adjustments ?? ManCoverageAdjustments.None;
        var groups = new List<BunchSwitchGroup>();
        if (Adjustments.SwitchPairs)
        foreach (var group in Surface.Threats.Where(t => t.BunchGroup != null).GroupBy(t => t.BunchGroup!.Value))
        {
            var members = group.ToArray();
            if (members.Length != 2 || members.Any(t => Adjustments.Shadows.Any(s => s.Receiver == t.ActorId)
                || Adjustments.Bracket?.Receiver == t.ActorId)) continue;
            var owners = Assignments.Where(a => a.Pass is ManResponsibility m && members.Any(t => t.ActorId == m.Target))
                .OrderByDescending(a => MathF.Abs(a.Alignment.X - Surface.CenterX)).ThenBy(a => a.ActorId).ToArray();
            if (owners.Length == 2) groups.Add(new(group.Key, members[0].ActorId, members[1].ActorId, owners[0].ActorId, owners[1].ActorId));
        }
        SwitchGroups = groups.AsReadOnly();
    }
}

/// <summary>Passive preview actors and their typed jobs. Deliberately not PlaySetupResult:
/// legacy execution interprets receiver IDs as list indices and must never consume this adapter.</summary>
public sealed class DefensivePlanPreview
{
    public ResolvedDefensivePlan Plan { get; }
    public FormationResult Offense { get; }
    public IReadOnlyList<Defender> Defenders { get; }
    internal DefensivePlanPreview(ResolvedDefensivePlan plan, FormationResult offense, DefensiveTeamAttributes team)
    {
        DefensivePlanValidator.ThrowIfInvalid(plan);
        if (!plan.Offense.SameGeometry(OffensiveAlignmentSnapshot.From(offense, plan.Offense.LineOfScrimmage)))
            throw new ArgumentException("Preview offense differs from the compiled visible snapshot. Recompile before preview.");
        Plan = plan; Offense = offense;
        Defenders = Array.AsReadOnly(plan.Assignments.Select(a =>
            new Defender(a.Alignment, a.PositionRole, a.ActorId, team)).ToArray());
        PressureCompiler.ValidateDrops(plan, Defenders);
    }
}

using System.Numerics;

namespace RetroQB.Gameplay;

public enum SpecialTeamsPhase { Setup, Snap, Flight, Return, Result }
public sealed class SpecialTeamsPlayer(Vector2 position, string role)
{
    public Vector2 Position = position;
    public Vector2 Velocity;
    public string Role { get; } = role;
}
public sealed record KickReturnResult(float ReceivingYard, bool Touchback, bool Touchdown, int ReturnYards, bool Safety = false);

/// <summary>Playable punt/kickoff flight, coverage, blocking and return in kicking-team coordinates.</summary>
public sealed class SpecialTeamsPlay
{
    private static readonly float[] KickoffCoverageLanes = [-18f, -14f, -10f, -6f, -2f, 2f, 6f, 10f, 14f, 18f, 0f];
    private static readonly float[] PuntCoverageLanes = [-20f, -15f, -10f, -6f, -2f, 2f, 6f, 10f, 15f, 20f, 0f];
    public bool IsKickoff { get; }
    public SpecialTeamsPhase Phase { get; private set; }
    public List<SpecialTeamsPlayer> Coverage { get; } = new();
    public List<SpecialTeamsPlayer> Blockers { get; } = new();
    public SpecialTeamsPlayer Returner { get; }
    public SpecialTeamsPlayer Kicker { get; }
    public int ControlledCoverageIndex => 3;
    public Vector2 BallPosition { get; private set; }
    public float BallHeight { get; private set; }
    public float LineOfScrimmage { get; }
    public KickReturnResult? Result { get; private set; }
    public float Seconds { get; private set; }
    private float _phaseSeconds;
    private readonly Vector2 _launch, _landing;
    private float _catchY;
    private readonly SpecialTeamsPlayer[] _blockingPlayers;
    private readonly SpecialTeamsPlayer[] _coveragePlayers;
    private HashSet<int> _primaryPursuers = new();
    public SpecialTeamsPlay(bool kickoff, float ownYard, Random random)
    {
        IsKickoff = kickoff; LineOfScrimmage = ownYard + 10;
        float center = Constants.FieldWidth / 2;
        _launch = new(center, kickoff ? LineOfScrimmage : Math.Max(1, LineOfScrimmage - 14));
        float landingX = 10 + (float)random.NextDouble() * 33;
        // Explicit one-in-five touchback chance, independent of kick placement.
        bool touchback = kickoff && random.Next(5) == 0;
        float landingY = kickoff
            ? (touchback ? 110 : 90) + (float)random.NextDouble() * (touchback ? 3 : 15)
            : Math.Min(118, LineOfScrimmage + 36 + (float)random.NextDouble() * 14);
        _landing = new(landingX, landingY);
        Kicker = new(_launch - new Vector2(0, kickoff ? 3 : 0), kickoff ? "K" : "P");
        BallPosition = kickoff ? _launch : new(center, LineOfScrimmage);
        for (int i = 0; i < 10; i++)
        {
            // Punt: seven on the line (including two wide gunners), three protectors, and the punter.
            float x = kickoff ? 3 + i * (Constants.FieldWidth - 6) / 9
                : i >= 7 ? center + (i - 8) * 4
                : i is 0 or 6 ? 4 + i * 7.5f : center + (i - 3) * 2.6f;
            float y = LineOfScrimmage - (kickoff ? 1 : i >= 7 ? i == 8 ? 8 : 5 : 0);
            Coverage.Add(new(new(x, y), i == 3 ? "LB" : "ST"));
        }
        Returner = new(new(_landing.X, Math.Min(108, _landing.Y)), "KR");
        for (int i = 0; i < 10; i++)
        {
            float y = kickoff ? (i < 5 ? LineOfScrimmage + 12 : Returner.Position.Y - 14)
                : i < 7 ? LineOfScrimmage + 3 : Math.Max(LineOfScrimmage + 8, Returner.Position.Y - 13);
            float x = kickoff ? 8 + i % 5 * 9
                : i < 5 ? center + (i - 2) * 3
                : i < 7 ? 4 + (i - 5) * 45 : center + (i - 8) * 10;
            Blockers.Add(new(new(x, y), "BL"));
        }
        _coveragePlayers = Coverage.Append(Kicker).ToArray();
        _blockingPlayers = _coveragePlayers.Concat(Blockers).ToArray();
    }
    public void Start()
    {
        if (Phase != SpecialTeamsPhase.Setup) return;
        Phase = SpecialTeamsPhase.Snap; _phaseSeconds = 0;
    }
    public void Update(float dt, Vector2 movement, bool sprint, bool userReceiving)
    {
        if (!float.IsFinite(dt) || dt < 0) throw new ArgumentOutOfRangeException(nameof(dt));
        // Small steps prevent players tunneling through a block during a slow frame.
        while (dt > 0 && Phase is not (SpecialTeamsPhase.Setup or SpecialTeamsPhase.Result))
        {
            float step = Math.Min(dt, 1f / 120);
            UpdateStep(step, movement, sprint, userReceiving);
            dt -= step;
        }
    }
    private void UpdateStep(float dt, Vector2 movement, bool sprint, bool userReceiving)
    {
        if (Phase is SpecialTeamsPhase.Setup or SpecialTeamsPhase.Result) return;
        Seconds += dt; _phaseSeconds += dt;
        if (movement.LengthSquared() > 1) movement = Vector2.Normalize(movement);
        if (Phase == SpecialTeamsPhase.Snap)
        {
            float duration = IsKickoff ? .65f : .55f;
            if (IsKickoff) Kicker.Position = Vector2.Lerp(_launch - new Vector2(0, 3), _launch, Math.Min(1, _phaseSeconds / duration));
            else BallPosition = Vector2.Lerp(new(Constants.FieldWidth / 2, LineOfScrimmage), _launch, Math.Min(1, _phaseSeconds / duration));
            if (_phaseSeconds >= duration) { Phase = SpecialTeamsPhase.Flight; _phaseSeconds = 0; BallPosition = _launch; }
            return;
        }
        if (Phase == SpecialTeamsPhase.Flight)
        {
            float progress = Math.Min(1, _phaseSeconds / (IsKickoff ? 3.1f : 2.6f));
            BallPosition = Vector2.Lerp(_launch, _landing, progress); BallHeight = 5 * MathF.Sin(progress * MathF.PI);
            MoveCoverage(dt, userReceiving, movement, sprint, _landing);
            if (progress >= 1)
            {
                BallHeight = 0;
                if (_landing.Y >= 110) { Finish(20, true); return; }
                Returner.Position = _landing; _catchY = _landing.Y;
                AssignReturnCoverageRoles(userReceiving);
                Phase = SpecialTeamsPhase.Return; _phaseSeconds = 0;
            }
            return;
        }
        Vector2 direction = movement;
        if (!userReceiving)
        {
            var nearest = _coveragePlayers.MinBy(p => Vector2.DistanceSquared(p.Position, Returner.Position))!;
            float evade = Vector2.Distance(nearest.Position, Returner.Position) < 10 ? MathF.Sign(Returner.Position.X - nearest.Position.X) : 0;
            if (Returner.Position.X < 5) evade = 1;
            if (Returner.Position.X > Constants.FieldWidth - 5) evade = -1;
            direction = Vector2.Normalize(new Vector2(evade * .7f, -1));
        }
        Returner.Velocity = direction * (userReceiving && !sprint ? 7.3f : 9f);
        Returner.Position += Returner.Velocity * dt; BallPosition = Returner.Position;
        if (Returner.Position.Y <= 10) { Finish(100, false, true); return; }
        if (Returner.Position.Y >= 110) { Finish(0, safety: true); return; }
        if (Returner.Position.X <= 0 || Returner.Position.X >= Constants.FieldWidth)
        { Finish(Math.Clamp(110 - Returner.Position.Y, 1, 99)); return; }
        MoveCoverage(dt, userReceiving, movement, sprint, Returner.Position);
        if (_coveragePlayers.Any(p => Vector2.DistanceSquared(p.Position, Returner.Position) < 2.25f))
            Finish(Math.Clamp(110 - Returner.Position.Y, 1, 99));
    }
    private void MoveCoverage(float dt, bool userReceiving, Vector2 movement, bool sprint, Vector2 target)
    {
        foreach (var blocker in Blockers)
        {
            var threat = _coveragePlayers.MinBy(p => Vector2.DistanceSquared(p.Position, blocker.Position))!;
            blocker.Velocity = Toward(blocker.Position, threat.Position) * 5.4f;
            blocker.Position += blocker.Velocity * dt;
        }
        // Once the ball is kicked, the kicker/punter joins the same coverage and contact rules.
        for (int i = 0; i < _coveragePlayers.Length; i++)
        {
            var player = _coveragePlayers[i];
            var controlledByUser = !userReceiving && i == ControlledCoverageIndex;
            var pursuitTarget = GetCoverageTarget(i, target);
            var direction = controlledByUser ? movement : Toward(player.Position, pursuitTarget);
            float speed = !userReceiving && i == ControlledCoverageIndex && sprint ? 9 : 7.1f;
            if (Blockers.Any(b => Vector2.DistanceSquared(b.Position, player.Position) < 5)) speed *= .38f;
            player.Velocity = direction * speed;
            player.Position += player.Velocity * dt;
            player.Position = new(Math.Clamp(player.Position.X, 0, Constants.FieldWidth), Math.Clamp(player.Position.Y, 1, 119));
        }
        var players = _blockingPlayers;
        for (int iteration = 0; iteration < 6; iteration++)
        for (int i = 0; i < players.Length; i++)
        for (int j = i + 1; j < players.Length; j++)
        {
            var a = players[i]; var b = players[j];
            var delta = b.Position - a.Position;
            float distance = delta.Length();
            const float separation = 1.8f;
            if (distance >= separation) continue;
            var normal = distance > .001f ? delta / distance : Vector2.UnitX;
            var correction = normal * ((separation - distance) / 2);
            a.Position -= correction; b.Position += correction;
            // Cancel inward motion; players can still slide around a block.
            float closing = Vector2.Dot(b.Velocity - a.Velocity, normal);
            if (closing < 0) { a.Velocity += normal * closing / 2; b.Velocity -= normal * closing / 2; }
        }
    }

    private void AssignReturnCoverageRoles(bool userReceiving)
    {
        _primaryPursuers = _coveragePlayers
            .Select((player, index) => (player, index))
            .Where(pair => userReceiving || pair.index != ControlledCoverageIndex)
            .OrderBy(pair => Vector2.DistanceSquared(pair.player.Position, Returner.Position))
            .Take(2)
            .Select(pair => pair.index)
            .ToHashSet();
    }

    private Vector2 GetCoverageTarget(int index, Vector2 returnTarget)
    {
        if (_primaryPursuers.Contains(index)) return returnTarget;

        // Gunners and outside coverage keep the widest lanes; interior players
        // fill the staggered gaps. Because lanes move with the returner, coverage
        // fans out and then closes the escape routes instead of forming a mob.
        float offset = (IsKickoff ? KickoffCoverageLanes : PuntCoverageLanes)[index];
        if (Phase == SpecialTeamsPhase.Flight)
        {
            float spread = IsKickoff ? 0.55f : 0.42f;
            return new(Math.Clamp(_landing.X + offset * spread, 2f, Constants.FieldWidth - 2f),
                Math.Max(1f, _landing.Y - 3f));
        }

        return new(Math.Clamp(returnTarget.X + offset, 1.5f, Constants.FieldWidth - 1.5f),
            Math.Min(119f, returnTarget.Y + 2.5f));
    }
    private static Vector2 Toward(Vector2 from, Vector2 to)
        => Vector2.DistanceSquared(from, to) < .01f ? Vector2.Zero : Vector2.Normalize(to - from);
    private void Finish(float yard, bool touchback = false, bool touchdown = false, bool safety = false)
    {
        Result = new(yard, touchback, touchdown, touchback ? 0 : (int)MathF.Round(_catchY - Returner.Position.Y), safety);
        Phase = SpecialTeamsPhase.Result; Returner.Velocity = Vector2.Zero;
    }
}

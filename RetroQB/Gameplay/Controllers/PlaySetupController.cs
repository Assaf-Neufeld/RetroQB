using System.Numerics;
using RetroQB.AI;
using RetroQB.Core;
using RetroQB.Entities;

namespace RetroQB.Gameplay.Controllers;

/// <summary>
/// Responsible for setting up entities at the start of each play.
/// Handles formation creation, defense setup, and entity initialization.
/// </summary>
public sealed class PlaySetupController
{
    private readonly IFormationFactory _formationFactory;
    private readonly IDefenseFactory _defenseFactory;
    private readonly Random _rng;

    public PlaySetupController(
        IFormationFactory formationFactory,
        IDefenseFactory defenseFactory,
        DefensiveCoordinator defensiveCoordinator,
        Random rng)
    {
        _formationFactory = formationFactory;
        _defenseFactory = defenseFactory;
        _rng = rng;
    }

    /// <summary>
    /// Creates and returns all entities needed for a play.
    /// </summary>
    public PlaySetupResult SetupPlay(
        ResolvedPlay selectedPlay,
        DefensiveContext context,
        DefensiveCallDecision call,
        OffensiveTeamAttributes offensiveTeam,
        DefensiveTeamAttributes defensiveTeam)
    {
        // Create offensive formation
        var formationResult = _formationFactory.CreateFormation(selectedPlay, context.LineOfScrimmage, offensiveTeam);

        // Assign routes before building coverage matchups. The pre-snap surface
        // still includes blocking skill players when determining alignment/personnel.
        RouteAssigner.AssignRoutes(formationResult.Receivers, selectedPlay);

        DefensivePersonnel personnel = DefensivePersonnelPolicy.Create(formationResult.Receivers, context);

        // Create defense using the pre-decided scheme and personnel-aware blitz.
        var defenseResult = _defenseFactory.CreateDefense(
            context,
            call,
            personnel,
            formationResult.Receivers,
            _rng,
            defensiveTeam);

        return new PlaySetupResult(
            formationResult.Qb,
            formationResult.Ball,
            formationResult.Receivers,
            formationResult.Blockers,
            defenseResult.Defenders,
            defenseResult.UsesZoneResponsibilities,
            defenseResult.IsUnderneathManCoverage,
            defenseResult.Blitzers,
            defenseResult.Scheme);
    }

    public PlaySetupResult SetupPlay(ResolvedPlay selectedPlay, float lineOfScrimmage, ResolvedDefensivePlay defense,
        OffensiveTeamAttributes offensiveTeam, DefensiveTeamAttributes defensiveTeam)
    {
        var formation = _formationFactory.CreateFormation(selectedPlay, lineOfScrimmage, offensiveTeam);
        RouteAssigner.AssignRoutes(formation.Receivers, selectedPlay);
        var result = defense.CreateDefense(defensiveTeam);
        return new(formation.Qb, formation.Ball, formation.Receivers, formation.Blockers, result.Defenders,
            result.UsesZoneResponsibilities, result.IsUnderneathManCoverage, result.Blitzers, result.Scheme);
    }
}

/// <summary>
/// Contains all entities and configuration for a play setup.
/// </summary>
public sealed class PlaySetupResult
{
    public Quarterback Qb { get; }
    public Ball Ball { get; }
    public List<Receiver> Receivers { get; }
    public List<Blocker> Blockers { get; }
    public List<Defender> Defenders { get; }
    public bool UsesZoneResponsibilities { get; }
    public bool IsUnderneathManCoverage { get; }
    public List<string> Blitzers { get; }
    public CoverageScheme CoverageScheme { get; }

    public PlaySetupResult(
        Quarterback qb,
        Ball ball,
        List<Receiver> receivers,
        List<Blocker> blockers,
        List<Defender> defenders,
        bool usesZoneResponsibilities,
        bool isUnderneathManCoverage,
        List<string> blitzers,
        CoverageScheme coverageScheme)
    {
        Qb = qb;
        Ball = ball;
        Receivers = receivers;
        Blockers = blockers;
        Defenders = defenders;
        UsesZoneResponsibilities = usesZoneResponsibilities;
        IsUnderneathManCoverage = isUnderneathManCoverage;
        Blitzers = blitzers;
        CoverageScheme = coverageScheme;
    }
}

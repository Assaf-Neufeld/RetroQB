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
        PlayDefinition selectedPlay,
        DefensiveContext context,
        OffensiveTeamAttributes offensiveTeam,
        DefensiveTeamAttributes defensiveTeam)
    {
        // Create offensive formation
        var formationResult = _formationFactory.CreateFormation(selectedPlay, context.LineOfScrimmage, offensiveTeam);

        // Create defense
        var defenseResult = _defenseFactory.CreateDefense(
            context,
            formationResult.Receivers,
            _rng,
            defensiveTeam);

        // Assign routes to receivers
        RouteAssigner.AssignRoutes(formationResult.Receivers, selectedPlay, _rng);

        return new PlaySetupResult(
            formationResult.Qb,
            formationResult.Ball,
            formationResult.Receivers,
            formationResult.Blockers,
            defenseResult.Defenders,
            defenseResult.IsZoneCoverage,
            defenseResult.Blitzers,
            defenseResult.Scheme);
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
    public bool IsZoneCoverage { get; }
    public List<string> Blitzers { get; }
    public CoverageScheme CoverageScheme { get; }

    public PlaySetupResult(
        Quarterback qb,
        Ball ball,
        List<Receiver> receivers,
        List<Blocker> blockers,
        List<Defender> defenders,
        bool isZoneCoverage,
        List<string> blitzers,
        CoverageScheme coverageScheme)
    {
        Qb = qb;
        Ball = ball;
        Receivers = receivers;
        Blockers = blockers;
        Defenders = defenders;
        IsZoneCoverage = isZoneCoverage;
        Blitzers = blitzers;
        CoverageScheme = coverageScheme;
    }
}

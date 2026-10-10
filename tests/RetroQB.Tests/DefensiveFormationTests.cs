using System.Numerics;
using System.Text.Json;
using Raylib_cs;
using RetroQB.Data;
using RetroQB.Development;
using RetroQB.Entities;
using RetroQB.Gameplay;
using RetroQB.Gameplay.Defense;
using RetroQB.Input;

namespace RetroQB.Tests;

public sealed class DefensiveFormationTests
{
    private static OffensiveAlignmentSnapshot Snapshot(string play = "pass.gun-trips.mills", bool flip = false, float los = 60)
        => OffensiveAlignmentSnapshot.From(new FormationFactory().CreateFormation(PlayResolver.Resolve(new PlayManager().Catalog[play], flip), los), los);

    [Fact]
    public void EveryAdvertisedFrontPersonnelCallCompilesAcrossCatalogAndFlips()
    {
        int legal = 0, rejected = 0;
        foreach (var play in new PlayManager().Catalog.Plays)
        foreach (bool flip in new[] { false, true })
        {
        var snapshot = OffensiveAlignmentSnapshot.From(new FormationFactory().CreateFormation(PlayResolver.Resolve(play,flip),109),109);
        foreach (var package in Enum.GetValues<DefensePackage>())
        foreach (var front in Enum.GetValues<DefensiveFront>())
        foreach (var call in DefensiveCallDefinition.All)
        {
            if (front != DefensiveFront.Even && call.RushCount > 4)
            {
                Assert.Contains("Even", Assert.Throws<ArgumentException>(() => DefensivePlanCompiler.Compile(snapshot, call, package, front: front)).Message);
                rejected++; continue;
            }
            var plan = DefensivePlanCompiler.Compile(snapshot, call, package, front: front);
            Assert.Empty(DefensivePlanValidator.Validate(plan)); Assert.Equal(front, plan.Front);
            Assert.Equal(11, plan.Assignments.Count);
            Assert.Equal(package == DefensePackage.Dime ? 6 : package == DefensePackage.Nickel ? 5 : 4, plan.Assignments.Count(a => a.ActorId.IsDefensiveBack()));
            Assert.Equal(plan.Surface.Gaps.Count, plan.Fits.Gaps.Count);
            Assert.Equal(DefensivePersonnelRoles.Controlled(package), plan.ControlledActor); legal++;
        }
        }
        Assert.Equal(30456, legal); Assert.Equal(5184, rejected);
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public void FrontsChangeNamedInteriorFitsAndWidePathsAgainstActualStrength(bool flip)
    {
        var s = Snapshot(flip: flip); var call = DefensiveCallDefinition.Get("def.cover1");
        ResolvedDefensivePlan Plan(DefensiveFront f) => DefensivePlanCompiler.Compile(s, call, DefensePackage.Dime, front: f);
        var even = Plan(DefensiveFront.Even); var over = Plan(DefensiveFront.Over); var under = Plan(DefensiveFront.Under);
        var shortFront = Plan(DefensiveFront.ShortYardage); var wide = Plan(DefensiveFront.Wide);
        bool strongLeft = even.Surface.Strength == FieldSide.Left;
        var strong = strongLeft ? DefenderSlot.DT1 : DefenderSlot.DT2;
        var weak = strongLeft ? DefenderSlot.DT2 : DefenderSlot.DT1;
        float Gap(ResolvedDefensivePlan p, DefenderSlot a) => MathF.Abs(p.Surface.Gaps.Single(g => g.Index == ((RushResponsibility)p.Assignments.Single(j => j.ActorId == a).Pass).GapIndex).X - p.Surface.CenterX);
        Assert.True(Gap(over,strong) > Gap(under,strong)); Assert.True(Gap(over,weak) < Gap(under,weak));
        Assert.Equal(Gap(under,strong),Gap(shortFront,strong)); Assert.Equal(Gap(over,weak),Gap(shortFront,weak));
        Assert.True(wide.Assignments.Single(a=>a.ActorId==DefenderSlot.DE1).Alignment.X < even.Assignments.Single(a=>a.ActorId==DefenderSlot.DE1).Alignment.X);
        Assert.True(wide.Assignments.Single(a=>a.ActorId==DefenderSlot.DE2).Alignment.X > even.Assignments.Single(a=>a.ActorId==DefenderSlot.DE2).Alignment.X);
        Assert.All(shortFront.Pressure.Entries,e=>Assert.Contains(shortFront.Fits.Gaps,g=>g.Owner==e.Actor&&g.GapIndex==e.Gap));
        var cache = new DefensivePlanCache(); Assert.NotSame(cache.Resolve(s,call),cache.Resolve(s,call,front:DefensiveFront.Wide));
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public void DimeControlsAndFrontKeysKeepOffenseClocksAndRecordedIdentity(bool fullMatch)
    {
        var keys = new HashSet<KeyboardKey>(); var keyboard = new InputManager(keys.Contains);
        var input = new ScriptedInput(); var user = TeamCatalog.Get("ballers"); var cpu = TeamCatalog.Get("scarlet-guard");
        var full = new FullMatchSession(input,101,new(user,cpu,cpu.Id),DefensiveEngineMode.MatchDevelopment);
        var drive = fullMatch ? full.Drive : new DefensiveDrive(input,101,fixedCall:"pass.gun-trips.mills",engine:DefensiveEngineMode.MatchDevelopment);
        var session = new DefensivePlaySession(drive,101);
        void Update(bool pause=false, bool focus=true) { if(fullMatch) full.Update(0,new(Pause:pause,Focused:focus,DefensivePersonnel:keyboard.IsDefensivePersonnelPressed(),DefensiveFront:keyboard.IsDefensiveFrontPressed())); else session.Update(0,new(Pause:pause,Focused:focus,Personnel:keyboard.IsDefensivePersonnelPressed(),Front:keyboard.IsDefensiveFrontPressed())); }
        var offense = drive.Actors.Receivers.ToArray(); var qb = drive.Actors.Qb; var clock = drive.Timed.Clock.Snapshot();
        double deadline = fullMatch ? full.SnapRemaining : session.SnapRemaining;
        keys.Add(KeyboardKey.H); Update(); Update(); Update(); keys.Clear();
        Assert.Equal(DefensePackage.Dime,drive.CompiledPlan!.Package); Assert.Equal(DefenderSlot.MLB,drive.Linebacker.Slot);
        Assert.Single(drive.Actors.Defenders,d=>d.Slot.IsLinebacker());
        var defenders = drive.Actors.Defenders.ToArray(); keys.Add(KeyboardKey.J); Update(); keys.Clear();
        Assert.Equal(DefensiveFront.Over,drive.CompiledPlan.Front); Assert.Equal(defenders,drive.Actors.Defenders);
        Assert.Equal(offense,drive.Actors.Receivers); Assert.Same(qb,drive.Actors.Qb); Assert.Equal(clock,drive.Timed.Clock.Snapshot());
        Assert.Equal(deadline,fullMatch ? full.SnapRemaining : session.SnapRemaining);
        var plan = drive.CompiledPlan;
        Assert.False(drive.SelectDefense("def.edge")); Assert.Same(plan,drive.CompiledPlan); Assert.Contains("Even",drive.SelectionRejection);
        Update(pause:true); keys.Add(KeyboardKey.H); Update(); Assert.Same(plan,drive.CompiledPlan); keys.Clear(); Update(pause:true);
        keys.Add(KeyboardKey.H); Update(focus:false); Assert.Same(plan,drive.CompiledPlan); keys.Clear(); Update();
        Assert.True(drive.Snap()); keys.Add(KeyboardKey.H); Update(); Assert.Same(plan,drive.CompiledPlan); keys.Clear();
        for(int i=0;i<1800&&drive.Live;i++) drive.Update(1f/60);
        Assert.False(drive.Live); Assert.Equal(DefenderSlot.MLB,drive.LastReplay!.MatchContext!.CompiledPlan!.ControlledActor);
        Assert.Same(plan,drive.LastReplay!.MatchContext!.CompiledPlan);
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(plan,DefenseRedesignMatrix.Json));
        Assert.Equal("Over",json.RootElement.GetProperty("Front").GetString());
    }

    [Theory] [InlineData("def.cover3-match.v2")] [InlineData("def.quarters-match.v2")]
    public void DimeTripsUsesDistinctSlotBacksAndPreservesFiveOwners(string call)
    {
        var plan = DefensivePlanCompiler.Compile(Snapshot(),DefensiveCallDefinition.Get(call),DefensePackage.Dime);
        Assert.NotNull(plan.Match); Assert.Contains(plan.Match.Roles,r=>r.Actor==DefenderSlot.DB6);
        Assert.Contains(plan.Match.Roles,r=>r.Actor==DefenderSlot.NB);
        var coordinator = new CoverageCoordinator(plan);
        var receivers = plan.Offense.Skills.Select(r=>new ObservedReceiver(r.Id,r.Position+new Vector2(0,9),new(0,5),ReleaseDirection.Upfield,true,false,false,false,1)).ToArray();
        var actors = plan.Assignments.Select(a=>new DefenderMotion(a.ActorId,a.Alignment,Vector2.Zero,8,.65f,1,false)).ToArray();
        var sight = new DefensiveSighting(plan.Offense.Quarterback,new(BallState.HeldByQB,plan.Offense.Quarterback,Vector2.Zero,0,null),receivers.Select(r=>new VisibleMotion(r.Id,r.Position,r.Velocity,.6f)),actors);
        var intents = coordinator.Decide(new(1,sight,receivers,false,0,0),DefenderSlot.MLB);
        Assert.Equal(5,intents.Count(i=>i.Primary!=null)); Assert.Equal(5,intents.Where(i=>i.Primary!=null).Select(i=>i.Primary).Distinct().Count());
        Assert.Contains(intents,i=>i.Actor==DefenderSlot.MLB&&i.Primary!=null);
    }

    [Fact]
    public void AppendedDimeSlotHasCompleteProfilesAndRatingsApplyOnce()
    {
        Assert.Equal(11,(int)DefenderSlot.NB); Assert.Equal(12,(int)DefenderSlot.DB6);
        Assert.Equal(DefenderSlot.NB,JsonSerializer.Deserialize<DefenderSlot>("11"));
        Assert.True(DefenderSlot.DB6.IsDefensiveBack()); Assert.False(DefenderSlot.DB6.IsLinebacker());
        foreach(var team in TeamCatalog.Selectable.Concat(TeamCatalog.Opponents))
        {
            Assert.True(team.Defense.Roster.Defenders.ContainsKey(DefenderSlot.DB6));
            var profile = team.Defense.Roster.GetProfile(DefenderSlot.DB6);
            var actor = new Defender(Vector2.Zero,DefensivePosition.DB,DefenderSlot.DB6,team.Defense);
            Assert.Equal(Math.Clamp(profile.SpeedRating,.7f,1.4f),actor.SpeedMultiplier);
            Assert.Equal(profile.IsStarPlayer,actor.IsStarPlayer);
            Assert.Equal(team.Defense.GetEffectiveSpeed(DefensivePosition.DB)*actor.SpeedMultiplier,actor.Speed);
            Assert.Equal(Math.Clamp(profile.CoverageRating,.7f,1.4f),actor.InterceptionMultiplier);
        }
        var plan = DefensivePlanCompiler.Compile(Snapshot("pass.mesh"),DefensiveCallDefinition.Get("def.cover2-man"),DefensePackage.Dime);
        Assert.Equal(5,plan.Assignments.Count(a=>a.Pass is ManResponsibility));
        Assert.Equal(2,plan.Assignments.Count(a=>a.Pass is HelpResponsibility));
    }
}

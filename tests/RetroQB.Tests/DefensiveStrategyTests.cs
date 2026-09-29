using System.Numerics;
using System.Text.Json;
using RetroQB.Data;
using RetroQB.Development;
using RetroQB.Entities;
using RetroQB.Gameplay;
using RetroQB.Gameplay.Defense;

namespace RetroQB.Tests;

public sealed class DefensiveStrategyTests
{
    private static readonly DefensiveCallContext Neutral = new(2,6,50,0,180,false,3,3);
    private static OffensiveAlignmentSnapshot Snapshot(string play="pass.gun-trips.mills",bool flip=false)
        => OffensiveAlignmentSnapshot.From(new FormationFactory().CreateFormation(PlayResolver.Resolve(new PlayManager().Catalog[play],flip),60),60);
    private static ResolvedDefensivePlan Plan(string call="def.cover1",DefensePackage package=DefensePackage.Nickel,
        DefensiveFront front=DefensiveFront.Even,PressureKind kind=PressureKind.Base,bool spy=false)
        => DefensivePlanCompiler.Compile(Snapshot(),DefensiveCallDefinition.Get(call),package,adjustments:spy?new(spy:DefenderSlot.SS):null,pressure:new(kind),front:front);
    private static DefensiveOutcome Outcome(ResolvedDefensivePlan p,float? release=null,float? arrival=null,FieldSide? escape=null,bool screen=false,bool abandoned=false)
        => new("scarlet-guard",DefensivePlaycaller.PackageId(p),p.Call.Id,p.RequestedCall.Id,p.FrontId,p.Package,DefensivePlaycaller.PressureId(p),p.RuleVersion,
            Array.AsReadOnly(new[]{DefenderSlot.DE1}),arrival,release,escape,screen,Array.AsReadOnly(abandoned?new[]{DefenderSlot.OLB1}:Array.Empty<DefenderSlot>()));
    private static DefensiveStrategyMemory History(float? release=null,FieldSide? escape=null,bool screen=false)
    {
        var m=new DefensiveStrategyMemory(); var p=Plan();
        for(int i=1;i<=6;i++)m.Record(i,Outcome(p,release,escape:escape,screen:screen),5,PlayEndReason.Tackle);
        return m;
    }
    private static float Term(ResolvedDefensivePlan p,string rule,DefensiveCallContext? c=null,DefensiveStrategyMemory? m=null)
        => DefensivePlaycaller.Score(p,c??Neutral,TeamCatalog.Get("scarlet-guard").Defense,m??new()).Where(t=>t.Rule==rule).Sum(t=>t.Value);

    [Fact]
    public void CurrentOffensiveScriptFlagsCannotAlterCpuSelection()
    {
        var f=new FormationFactory().CreateFormation(PlayResolver.Resolve(new PlayManager().Catalog["pass.gun-trips.mills"]),60);
        var team=TeamCatalog.Get("scarlet-guard").Defense;var memory=History(release:3);
        var before=DefensivePlaycaller.Select(OffensiveAlignmentSnapshot.From(f,60),Neutral,team,memory,101,.42);
        foreach(var receiver in f.Receivers)
        { receiver.Route=RetroQB.Routes.RouteType.Wheel;receiver.RouteStart=Vector2.Zero;receiver.IsBlocking=!receiver.IsBlocking;receiver.Eligible=!receiver.Eligible; }
        var after=DefensivePlaycaller.Select(OffensiveAlignmentSnapshot.From(f,60),Neutral,team,memory,101,.42);
        Assert.Equal(JsonSerializer.Serialize(before,DefenseRedesignMatrix.Json),JsonSerializer.Serialize(after,DefenseRedesignMatrix.Json));
    }

    [Fact]
    public void ShortHeavyAndSpreadTermsRewardDeclaredFitsAndRealPersonnel()
    {
        var compact=Plan(package:DefensePackage.Base,front:DefensiveFront.ShortYardage); var wide=Plan(package:DefensePackage.Dime,front:DefensiveFront.Wide);
        Assert.True(Term(compact,"CALL-01",Neutral with {Distance=2})>Term(wide,"CALL-01",Neutral with {Distance=2}));
        Assert.Equal(0,Term(compact,"CALL-01"));
        Assert.True(Term(Plan("def.quarters-match.v2",DefensePackage.Dime),"CALL-03")>Term(Plan("def.cover2-man",DefensePackage.Base),"CALL-03"));
        Assert.Contains(compact.Assignments,a=>a.Pass is HelpResponsibility {Role:HelperRole.DeepMiddle});
    }

    [Fact]
    public void LongYardageUsesPastSlowReleasesAndQuickScreenHistoryPenalizesLatePressure()
    {
        var delayed=Plan(kind:PressureKind.Delayed); var zone=Plan("def.cover3"); var longYard=Neutral with {Distance=12};
        Assert.Equal(0,Term(delayed,"CALL-02",longYard));
        Assert.True(Term(delayed,"CALL-02",longYard,History(release:3))>0);
        Assert.Equal(0,Term(delayed,"CALL-05"));
        var quick=History(release:1,screen:true);
        Assert.True(Term(zone,"CALL-05",m:quick)>0); Assert.True(Term(delayed,"CALL-05",m:quick)<0);
        Assert.Equal(0,Term(delayed,"CALL-02",Neutral,History(release:3)));
    }

    [Fact]
    public void EscapeHistoryRewardsOnlyRealBudgetedSpyAndStopsAffectingScoresAfterReset()
    {
        var spy=Plan(spy:true); var normal=Plan(); var memory=History(escape:FieldSide.Left);
        Assert.Single(spy.Assignments,a=>a.Pass is HelpResponsibility {Role:HelperRole.Spy});
        Assert.Equal(normal.Assignments.Count(a=>a.Pass is ManResponsibility),spy.Assignments.Count(a=>a.Pass is ManResponsibility));
        Assert.True(Term(spy,"CALL-04",m:memory)>Term(normal,"CALL-04",m:memory));
        Assert.True(memory.PressureAdjustment(DefensivePlaycaller.PackageId(normal))<0);
        memory.Reset();Assert.Equal(0,Term(spy,"CALL-04",m:memory));
    }

    [Fact]
    public void LateLeadAndCriticalConversionHaveDifferentObjectives()
    {
        var deep=Plan("def.cover2-man");var pressure=Plan("def.edge",kind:PressureKind.Edge);var zero=Plan("def.zero",kind:PressureKind.Zero);
        var lead=Neutral with {FinalPeriod=true,SecondsRemaining=60,ScoreMargin=7,OffenseTimeouts=0};
        Assert.True(Term(deep,"CALL-06",lead)>Term(pressure,"CALL-06",lead));Assert.True(Term(zero,"CALL-06",lead)<0);
        Assert.Equal(0,Term(deep,"CALL-06",Neutral));
        var critical=lead with {SecondsRemaining=30,ScoreMargin=2,Yard=75,Distance=2,Down=3};
        Assert.Equal(0,Term(deep,"CALL-06",critical)); Assert.True(Term(pressure,"CALL-07",critical)>Term(zero,"CALL-07",critical));
        Assert.True(Term(pressure,"CALL-07",lead with {ScoreMargin=-3,DefenseTimeouts=0})>0);
    }

    [Fact]
    public void ExactPackageLearningIsBoundedIdempotentAndDoesNotCreditPlainIncompletionsToPressure()
    {
        var p=Plan("def.edge",kind:PressureKind.Edge);var m=new DefensiveStrategyMemory();var outcome=Outcome(p,1);
        m.Record(1,outcome,0,PlayEndReason.Incomplete);m.Record(1,outcome,0,PlayEndReason.Incomplete);
        Assert.Equal(1,m.CompletedPlays);Assert.Equal(0,m.PressureAdjustment(outcome.PackageId));
        Assert.InRange(m.CoverageAdjustment(outcome.PackageId),0,.01f);
        Assert.Equal(0,m.Adjustment(DefensivePlaycaller.PackageId(Plan())));
        for(int i=2;i<=100;i++)m.Record(i,Outcome(p,3,1),-5,PlayEndReason.Sack);
        Assert.InRange(m.Adjustment(outcome.PackageId),0,.6f);Assert.True(m.PressureAdjustment(outcome.PackageId)>0);
        Assert.Equal(12,m.History.Samples);
        var before=m.Adjustment(outcome.PackageId);m.Record(101,Outcome(p,abandoned:true),50,PlayEndReason.Touchdown);
        Assert.Equal(before,m.Adjustment(outcome.PackageId));
        m.Reset();Assert.Equal(0,m.CompletedPlays);Assert.Equal(0,m.Adjustment(outcome.PackageId));
    }

    [Fact]
    public void SelectionIsLegalDeterministicExplainableAndPreservesVariety()
    {
        var s=Snapshot();var team=TeamCatalog.Get("scarlet-guard").Defense;var memory=new DefensiveStrategyMemory();
        var a=DefensivePlaycaller.Select(s,Neutral,team,memory,101,.4);
        var b=DefensivePlaycaller.Select(new(s.Ball,s.Quarterback,s.LineOfScrimmage,s.Skills.Reverse(),s.Linemen.Reverse()),Neutral,team,memory,101,.4);
        Assert.Equal(JsonSerializer.Serialize(a,DefenseRedesignMatrix.Json),JsonSerializer.Serialize(b,DefenseRedesignMatrix.Json));
        Assert.Empty(DefensivePlanValidator.Validate(a.Plan));Assert.All(a.Candidates,c=>Assert.True(c.Weight>0&&double.IsFinite(c.Weight)));
        Assert.Contains(a.Candidates,c=>c.Personnel==DefensePackage.Dime);Assert.Contains(a.Candidates,c=>c.PackageId.EndsWith("/SS"));
        Assert.Contains(a.Rejections,r=>r.Contains("nickel or dime"));
        var picks=new[]{.01,.2,.5,.8,.99}.Select(draw=>DefensivePlaycaller.Select(s,Neutral,team,memory,101,draw).Plan.Call.Id).Distinct().Count();
        Assert.True(picks>=3);
        var p=a.Plan;var human=DefensivePlanCompiler.Compile(s,p.RequestedCall,p.Package,p.ResolutionSeed,p.Adjustments,p.Pressure.Selection,p.Front);
        Assert.Equal(JsonSerializer.Serialize(p,DefenseRedesignMatrix.Json),JsonSerializer.Serialize(human,DefenseRedesignMatrix.Json));
    }

    [Theory] [InlineData(30)] [InlineData(60)] [InlineData(120)]
    public void LiveCpuSelectionSurvivesPreviewBrowsingAndRecordsOnceForDefendingTeam(int hz)
    {
        var input=new ScriptedInput();var user=TeamCatalog.Get("ballers");var cpu=TeamCatalog.Get("scarlet-guard");
        var drive=new DefensiveDrive(input,101,fixedCall:"pass.gun-trips.mills",match:new(user,cpu,user.Id),engine:DefensiveEngineMode.StrategyDevelopment);
        var initial=JsonSerializer.Serialize(drive.CompiledPlan,DefenseRedesignMatrix.Json);double draw=drive.CpuSelection!.Draw;
        drive.FlipOffensiveFormationForDevelopment();Assert.Equal(draw,drive.CpuSelection!.Draw);drive.FlipOffensiveFormationForDevelopment();
        Assert.Equal(initial,JsonSerializer.Serialize(drive.CompiledPlan,DefenseRedesignMatrix.Json));
        Assert.True(drive.Snap());
        for(int i=0;i<hz*30&&drive.Live;i++){input.Frame=new(Movement:Vector2.UnitY,ThrowTarget:i>=hz?0:null);drive.Update(1f/hz);}
        Assert.False(drive.Live);var result=drive.LastResult!;var metadata=result.Event.DefenseOutcome!;
        Assert.Equal(cpu.Id,metadata.DefenseId);Assert.Equal(DefensivePlaycaller.PackageId(drive.CompiledPlan!),metadata.PackageId);
        Assert.NotEmpty(metadata.ActualRushers);Assert.Equal(1,drive.Match.Opponent.DefenseStrategy.CompletedPlays);Assert.Equal(0,drive.Match.User.DefenseStrategy.CompletedPlays);
        Assert.Same(result,drive.Match.Resolve(result.Event));Assert.Equal(1,drive.Match.Opponent.DefenseStrategy.CompletedPlays);
        Assert.NotNull(drive.LastReplay!.MatchContext!.CpuSelection);
        drive.Restart();Assert.Equal(0,drive.Match.Opponent.DefenseStrategy.CompletedPlays);Assert.Equal(initial,JsonSerializer.Serialize(drive.CompiledPlan,DefenseRedesignMatrix.Json));
    }

    [Fact]
    public void WrongDefendingTeamMetadataCannotMutateScoreOrMemory()
    {
        var user=TeamCatalog.Get("ballers");var cpu=TeamCatalog.Get("scarlet-guard");var match=new MatchState(user,cpu,user.Id);
        var play=match.BeginPlay("fixture");var outcome=Outcome(Plan()) with {DefenseId=user.Id};
        Assert.Throws<ArgumentException>(()=>match.Resolve(new(play.Id,user.Id,PlayEndReason.Tackle,25,DefenseOutcome:outcome)));
        Assert.Empty(match.History);Assert.Equal(0,match.Opponent.DefenseStrategy.CompletedPlays);Assert.NotNull(match.ActivePlay);
        match.Resolve(new(play.Id,user.Id,PlayEndReason.Tackle,25,DefenseOutcome:outcome with {DefenseId=cpu.Id}));
        match.StartPossession(cpu.Id,new()); var next=match.BeginPlay("fixture.other-side");
        match.Resolve(new(next.Id,cpu.Id,PlayEndReason.Tackle,25,DefenseOutcome:outcome));
        Assert.Equal(1,match.User.DefenseStrategy.CompletedPlays);Assert.Equal(1,match.Opponent.DefenseStrategy.CompletedPlays);
    }

    [Fact]
    public void TelemetryCountsActualRushArrivalAndConditionalTransitionWithoutPromisingManualParticipation()
    {
        var plan=Plan();var tracker=new DefensiveOutcomeTracker(plan,"scarlet-guard");
        var actor=new DefenderMotion(DefenderSlot.DE1,plan.Offense.Quarterback+new Vector2(0,2),Vector2.Zero,7,1,1,false);
        var sight=new DefensiveSighting(plan.Offense.Quarterback,new(BallState.HeldByQB,plan.Offense.Quarterback,Vector2.Zero,0,null),[],[actor]);
        var obs=new DefensiveObservationFrame(1,sight,[],false,0,0);
        var frame=new DefensiveDecisionFrame(DefensiveRuntime.Version,1,obs,[],["PRESS-09:DelayAndVisibleTriggerSatisfied"])
            {Pressure=new(4,0,[new(DefenderSlot.DE1,0,PressureStage.Approach,Vector2.Zero,ContainRole.None,true,false,true,"Manual idle")])};
        tracker.Observe(frame,BallState.HeldByQB,null);Assert.Empty(tracker.Complete().ActualRushers);Assert.Null(tracker.Complete().PressureArrivalSeconds);
        frame=frame with {Pressure=new(4,1,[new(DefenderSlot.DE1,0,PressureStage.Close,Vector2.Zero,ContainRole.None,true,true,false,"Actual movement")])};
        tracker.Observe(frame,BallState.HeldByQB,null);tracker.Observe(frame with {Seconds=2},BallState.InAir,null);
        var result=tracker.Complete();Assert.Equal(new[]{DefenderSlot.DE1},result.ActualRushers);Assert.Equal(1,result.PressureArrivalSeconds);Assert.Equal(2,result.ThrowSeconds);
        Assert.Single(result.PressureTransitions);
    }

    [Fact]
    public void ObservedHumanDepartureIsRecordedSeparatelyAndTeamIdentityChangesPressurePreference()
    {
        var p=Plan();var tracker=new DefensiveOutcomeTracker(p,"scarlet-guard");var id=p.Offense.Skills[0].Id;
        var receiver=new ObservedReceiver(id,new(20,60),Vector2.Zero,ReleaseDirection.Unknown,false,false,false,false,1);
        var actor=new DefenderMotion(DefenderSlot.OLB1,new(20,80),Vector2.UnitY*5,7,1,1,false);
        var sight=new DefensiveSighting(p.Offense.Quarterback,new(BallState.HeldByQB,p.Offense.Quarterback,Vector2.Zero,0,null),
            [new(id,receiver.Position,receiver.Velocity,1)],[actor]);
        var frame=new DefensiveDecisionFrame(DefensiveRuntime.Version,2,new(2,sight,[receiver],false,0,0),
            [new(DefenderSlot.OLB1,DefensiveIntentKind.Leverage,receiver.Position,id,null,"MAN-02","Primary",false)],[]);
        tracker.Observe(frame,BallState.HeldByQB,DefenderSlot.OLB1);
        Assert.Equal(new[]{DefenderSlot.OLB1},tracker.Complete().HumanAbandonments);
        var pressure=Plan("def.edge",kind:PressureKind.Edge);
        float Team(DefensiveTeamAttributes team)=>DefensivePlaycaller.Score(pressure,Neutral,team,new()).Single(t=>t.Rule=="TEAM").Value;
        Assert.True(Team(DefensiveTeamPresets.CrimsonRush)>Team(TeamCatalog.Get("sentinels").Defense));
    }
}

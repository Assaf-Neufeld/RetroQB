using RetroQB.Entities;

namespace RetroQB.Gameplay.Defense;

public sealed record DefensiveCallContext(int Down, float Distance, float Yard, int ScoreMargin,
    double SecondsRemaining, bool FinalPeriod, int OffenseTimeouts, int DefenseTimeouts);
public sealed record DefensiveScoreTerm(string Rule, float Value, string Reason);
public sealed record DefensiveCallScore(string PackageId, string CallId, DefensePackage Personnel,
    DefensiveFront Front, string PressureId, float Score, double Weight, IReadOnlyList<DefensiveScoreTerm> Terms);
public sealed record DefensiveCallSelection(ResolvedDefensivePlan Plan, double Draw,
    IReadOnlyList<DefensiveCallScore> Candidates, IReadOnlyList<string> Rejections)
{
    public string Version => "defense-caller-p9-v1";
}

/// <summary>Scores only legal, completely compiled defenses using public situation and completed observations.</summary>
public static class DefensivePlaycaller
{
    public static string PressureId(ResolvedDefensivePlan p) => "pressure."+p.Pressure.Selection.Kind.ToString().ToLowerInvariant()+"."+p.Pressure.Selection.Side.ToString().ToLowerInvariant()+".v1";
    public static string PackageId(ResolvedDefensivePlan p) => string.Join("/",p.Call.Id,p.Package,p.FrontId,PressureId(p),p.Adjustments.Spy?.ToString()??"no-spy");

    public static DefensiveCallSelection Select(OffensiveAlignmentSnapshot offense, DefensiveCallContext context,
        DefensiveTeamAttributes team, DefensiveStrategyMemory memory, int seed, double draw)
    {
        if(!double.IsFinite(draw)||draw<0||draw>=1) throw new ArgumentOutOfRangeException(nameof(draw));
        var plans = new Dictionary<string,ResolvedDefensivePlan>(StringComparer.Ordinal); var rejected=new List<string>();
        foreach(var call in DefensiveCallDefinition.All)
        foreach(var package in Enum.GetValues<DefensePackage>())
        foreach(var front in Enum.GetValues<DefensiveFront>())
        {
            // Non-even fronts intentionally expose the base four-man package only.
            if(front!=DefensiveFront.Even&&call.RushCount>4) continue;
            Add(call,package,front,PressureSelection.For(call),false);
            if(front!=DefensiveFront.Even) continue;
            var kinds=call.Id=="def.cover1"?new[]{PressureKind.GreenDog,PressureKind.Delayed,PressureKind.Stunt}
                :call.Id=="def.edge"?new[]{PressureKind.Mike,PressureKind.Nickel}
                :call.Id=="def.cover3"?new[]{PressureKind.ZoneFive,PressureKind.Simulated}:Array.Empty<PressureKind>();
            foreach(var kind in kinds) foreach(var side in new[]{FieldSide.Left,FieldSide.Right}) Add(call,package,front,new(kind,side),false);
            if(call.RushCount>4) Add(call,package,front,PressureSelection.For(call).Flip(),false);
            if(call.Id=="def.cover1") Add(call,package,front,new(PressureKind.Base),true);
        }
        var scored=plans.OrderBy(p=>p.Key,StringComparer.Ordinal).Select(p=>(Plan:p.Value,Terms:Score(p.Value,context,team,memory))).ToArray();
        if(scored.Length==0) throw new InvalidOperationException("No legal defensive candidate.");
        // Adding more front/pressure variants must not itself make that coverage family more frequent.
        var counts=scored.GroupBy(p=>p.Plan.Call.Id).ToDictionary(g=>g.Key,g=>g.Count());
        var rows=scored.Select(p=>new DefensiveCallScore(PackageId(p.Plan),p.Plan.Call.Id,p.Plan.Package,p.Plan.Front,PressureId(p.Plan),
            p.Terms.Sum(t=>t.Value),Math.Exp(Math.Clamp(p.Terms.Sum(t=>t.Value),-4,4))/counts[p.Plan.Call.Id],p.Terms)).ToArray();
        double remaining=draw*rows.Sum(r=>r.Weight); int chosen=rows.Length-1;
        for(int i=0;i<rows.Length;i++) { remaining-=rows[i].Weight; if(remaining<0){chosen=i;break;} }
        return new(scored[chosen].Plan,draw,Array.AsReadOnly(rows),rejected.AsReadOnly());

        void Add(DefensiveCallDefinition call,DefensePackage package,DefensiveFront front,PressureSelection pressure,bool spy)
        {
            try
            {
                var p=DefensivePlanCompiler.Compile(offense,call,package,seed,
                    call.Match==MatchFamily.None&&call.Zone==SpotZoneFamily.None?new(switchPairs:true,spy:spy?DefenderSlot.SS:null):null,pressure,front);
                PressureCompiler.ValidateDrops(p,p.Assignments.Select(a=>new Defender(a.Alignment,a.PositionRole,a.ActorId,team)));
                plans.TryAdd(PackageId(p),p);
            }
            catch(ArgumentException e){rejected.Add($"{call.Id}/{package}/{front}/{pressure}: {e.Message}");}
            catch(InvalidOperationException e){rejected.Add($"{call.Id}/{package}/{front}/{pressure}: {e.Message}");}
        }
    }

    public static IReadOnlyList<DefensiveScoreTerm> Score(ResolvedDefensivePlan p,DefensiveCallContext c,
        DefensiveTeamAttributes team,DefensiveStrategyMemory memory)
    {
        var terms=new List<DefensiveScoreTerm>(); var h=memory.History;
        float evidence=MathF.Min(1,h.Samples/6f); bool pressure=p.Pressure.Entries.Count>4||p.Pressure.Conditional!=null||p.Pressure.Stunt!=null;
        bool twoHigh=p.Call.Shell==ManShell.TwoHigh; bool zone=p.Call.Zone!=SpotZoneFamily.None;
        bool compact=p.Front is DefensiveFront.ShortYardage or DefensiveFront.Under or DefensiveFront.Over;
        bool shortYard=c.Distance<=3; bool heavy=p.Offense.Linemen.Count>5||p.Surface.DetachedCount<=2;
        if(shortYard||heavy) Add("CALL-01",(compact?.65f:0)+(p.Package==DefensePackage.Base?.5f:p.Package==DefensePackage.Dime?-.45f:0)+(p.Front==DefensiveFront.Wide?-.65f:0),"Short/heavy: interior fits and retained shell");
        if(c.Distance>=8) Add("CALL-02",(twoHigh?.4f:0)+(pressure?.8f*h.SlowRate*evidence:0),"Long yardage and observed slow release rate");
        if(p.Surface.DetachedCount>=3)
        {
            float personnel=p.Package==DefensePackage.Dime?.65f:p.Package==DefensePackage.Nickel?.45f:-.35f;
            int exposed=p.Assignments.Count(a=>a.PositionRole==DefensivePosition.LB&&a.Pass is ManResponsibility m&&p.Surface.Threats.Any(t=>t.ActorId==m.Target&&!t.Attached&&!t.Backfield));
            Add("CALL-03",personnel+(p.Match?.Trips==true?.4f:0)-exposed*.2f,"Detached threats: DB personnel, supported trips check, visible LB mismatch");
        }
        if(h.EscapeRate>0) Add("CALL-04",evidence*h.EscapeRate*(p.Adjustments.Spy!=null?1.4f:p.Front==DefensiveFront.Wide?.5f:pressure?-.35f:.15f),"Observed escapes: actual spy/helper budget and contain paths");
        if(h.EscapeRate>0&&pressure&&p.Pressure.Selection.Side==h.EscapeSide&&p.Pressure.Selection.Kind is PressureKind.Edge or PressureKind.Nickel)
            Add("CALL-04",-.3f*evidence*h.EscapeRate,"Reduce repeated pressure-side escape exposure");
        if(h.QuickRate+h.ScreenRate>0) Add("CALL-05",evidence*MathF.Min(1,h.QuickRate+h.ScreenRate)*(p.Pressure.Selection.Kind is PressureKind.Delayed or PressureKind.Stunt?-.9f:zone?.55f:pressure?-.2f:.25f),"Past quick throws/screens: short contests over late pressure");
        bool conversionCritical=c.FinalPeriod&&c.SecondsRemaining<=45&&c.ScoreMargin is >0 and <=3&&c.Yard>=60&&c.Distance<=3&&c.Down>=3;
        if(c.FinalPeriod&&c.SecondsRemaining<=120&&c.ScoreMargin>0&&!conversionCritical)
            Add("CALL-06",(twoHigh?.9f:p.Call.Shell==ManShell.Zero?-1.2f:0)+(pressure?-.45f:0)+(zone&&c.OffenseTimeouts==0?.35f:0),"Late lead: deep/sideline protection with underneath concessions");
        if(c.FinalPeriod&&c.SecondsRemaining<=120&&(c.ScoreMargin<=0||conversionCritical))
            Add("CALL-07",(pressure&&p.Call.Shell!=ManShell.Zero?.7f:0)+(shortYard&&compact?.4f:0)+(p.Call.Shell==ManShell.Zero?-.3f:0)+(c.DefenseTimeouts==0&&pressure?.15f:0),"Urgent conversion stop; bounded pressure, not automatic Cover 0");
        Add("TEAM",Math.Clamp((pressure?team.BlitzFrequency-1:team.CoverageTightness-1)+(pressure?team.PassRushAbility-1:0)+(p.Package==DefensePackage.Base?team.TackleAbility-1:0),-.5f,.5f),"Defending team's actual strengths and tendencies");
        Add("CALL-08",memory.Adjustment(PackageId(p)),"Bounded exact-package outcomes; human abandonment excluded");
        return terms.AsReadOnly();
        void Add(string rule,float value,string reason)=>terms.Add(new(rule,value,reason));
    }
}

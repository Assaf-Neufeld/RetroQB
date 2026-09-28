using System.Numerics;
using System.Text.Json;
using Raylib_cs;
using RetroQB.Data;
using RetroQB.Development;
using RetroQB.Gameplay;
using RetroQB.Gameplay.Defense;
using RetroQB.Input;

namespace RetroQB.Tests;

public sealed class PressureInputTests
{
    [Theory] [InlineData(KeyboardKey.Nine, 8)] [InlineData(KeyboardKey.Zero, 9)]
    public void RealKeyboardMappingSelectsPressureAndKeepsCpuActorsAndDeadline(KeyboardKey key, int index)
    {
        var pressed = new HashSet<KeyboardKey> { key }; var keyboard = new InputManager(pressed.Contains);
        var drive = new DefensiveDrive(new ScriptedInput(), 101, fixedCall: "pass.mesh", engine: DefensiveEngineMode.ZoneDevelopment);
        var session = new DefensivePlaySession(drive, 101); var actors = drive.Players.ToArray();
        double deadline = session.SnapRemaining; var clock = session.Clock.Snapshot(); var offense = drive.Plays.SelectedPlay;
        Assert.Equal(index, keyboard.GetDefensivePlaySelection());
        session.Update(0, new(Call: keyboard.GetDefensivePlaySelection()));
        Assert.Equal(index == 8 ? "def.edge" : "def.zero", drive.CompiledPlan!.Call.Id);
        Assert.Equal(actors, drive.Players); Assert.Same(offense, drive.Plays.SelectedPlay);
        Assert.Equal(clock, session.Clock.Snapshot()); Assert.Equal(deadline, session.SnapRemaining);
        string original = JsonSerializer.Serialize(drive.CompiledPlan, DefenseRedesignMatrix.Json);
        pressed.Clear(); pressed.Add(KeyboardKey.X);
        session.Update(0, new(Flip: keyboard.IsFlipPlayPressed()));
        Assert.NotEqual(original, JsonSerializer.Serialize(drive.CompiledPlan, DefenseRedesignMatrix.Json));
        session.Update(0, new(Flip: keyboard.IsFlipPlayPressed()));
        Assert.Equal(original, JsonSerializer.Serialize(drive.CompiledPlan, DefenseRedesignMatrix.Json));
        Assert.Equal(actors, drive.Players); Assert.Equal(deadline, session.SnapRemaining);
        var expected = drive.CompiledPlan;
        drive.Snap(); Assert.Same(expected, drive.Execution.DefenseRuntime!.Plan);
        var manual = drive.Linebacker;
        drive.Update(1f / 120);
        // Shared collision separation may displace an idle actor; it must not gain AI movement.
        Assert.Same(manual, drive.Linebacker); Assert.Equal(Vector2.Zero, manual.Velocity);
        Assert.False(drive.Execution.DefenseRuntime.Current!.Decisions.Single(i => i.Actor == manual.Slot).ActiveRusher);
        Assert.True(drive.Execution.DefenseRuntime.Current.Pressure!.ActualCount < expected.Pressure.Entries.Count);
    }

    [Fact]
    public void VariantKeysFilterUnavailableRosterAndInvalidChangeKeepsLastPlan()
    {
        var drive = new DefensiveDrive(new ScriptedInput(), 101, fixedCall: "pass.pistol-twins.scissors", engine: DefensiveEngineMode.ZoneDevelopment);
        var session = new DefensivePlaySession(drive, 101); session.Update(0, new(Call: 8));
        Assert.Equal(DefensePackage.Base, drive.CompiledPlan!.Package);
        var keys = new HashSet<KeyboardKey> { KeyboardKey.E }; var keyboard = new InputManager(keys.Contains);
        var before = drive.CompiledPlan; var actors = drive.Players.ToArray();
        Assert.False(drive.SelectPressure(new(PressureKind.Nickel))); Assert.Same(before, drive.CompiledPlan);
        Assert.NotNull(drive.SelectionRejection); Assert.Equal(actors, drive.Players);
        session.Update(0, new(VariantStep: keyboard.GetDefensiveVariantStep()));
        Assert.Equal(PressureKind.Mike, drive.CompiledPlan.Pressure.Selection.Kind);
        Assert.Null(drive.SelectionRejection);
        Assert.Contains(drive.CompiledPlan.Pressure.Entries, e => e.Actor == drive.CompiledPlan.ControlledActor);
        keys.Clear(); keys.Add(KeyboardKey.Q);
        session.Update(0, new(VariantStep: keyboard.GetDefensiveVariantStep()));
        Assert.Equal(PressureKind.Edge, drive.CompiledPlan.Pressure.Selection.Kind);
        Assert.Equal(actors, drive.Players);
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public void FullMatchAndDefenseContextsPreserveClocksAndIgnoreSuspendedOrLiveChanges(bool fullMatch)
    {
        var input = new ScriptedInput(); var user = TeamCatalog.Get("ballers"); var cpu = TeamCatalog.Get("scarlet-guard");
        var full = new FullMatchSession(input, 101, new(user, cpu, cpu.Id), DefensiveEngineMode.ZoneDevelopment);
        var drive = fullMatch ? full.Drive : new DefensiveDrive(input, 101, fixedCall: "pass.mesh", engine: DefensiveEngineMode.ZoneDevelopment);
        var defense = new DefensivePlaySession(drive, 101);
        void Update(bool flip = false, int variant = 0, bool pause = false, bool focused = true, int? call = null)
        {
            if (fullMatch) full.Update(0, new(Call: call, Flip: flip, DefensiveVariantStep: variant, Pause: pause, Focused: focused));
            else defense.Update(0, new(Call: call, Flip: flip, VariantStep: variant, Pause: pause, Focused: focused));
        }
        Update(call: 8); var plan = drive.CompiledPlan!;
        double deadline = fullMatch ? full.SnapRemaining : defense.SnapRemaining;
        Update(pause: true); Update(flip: true, variant: 1, call: 9); Assert.Same(plan, drive.CompiledPlan);
        Update(pause: true); Update(flip: true, variant: 1, call: 9, focused: false); Assert.Same(plan, drive.CompiledPlan);
        Update(); Update(flip: true); Update(flip: true);
        Assert.Equal(JsonSerializer.Serialize(plan, DefenseRedesignMatrix.Json), JsonSerializer.Serialize(drive.CompiledPlan, DefenseRedesignMatrix.Json));
        Assert.Equal(deadline, fullMatch ? full.SnapRemaining : defense.SnapRemaining);
        drive.Snap(); plan = drive.CompiledPlan;
        Update(flip: true, variant: 1, call: 9); Assert.Same(plan, drive.CompiledPlan);
    }
}

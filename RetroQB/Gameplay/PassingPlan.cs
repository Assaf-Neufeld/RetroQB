using RetroQB.Entities;

namespace RetroQB.Gameplay;

public enum ProtectionMode { Balanced, SlideLeft, SlideRight, MaxProtect }

/// <summary>Authored progression is independent of the user's receiver targeting keys.</summary>
public sealed record PassRead(ReceiverSlot Slot, float EarliestSeconds = 0, int AfterStep = 0, bool Checkdown = false);

public sealed record ProtectionPlan(ProtectionMode Mode = ProtectionMode.Balanced, ReceiverSlot? HelpSlot = null)
{
    public ProtectionPlan Flip() => new(Mode switch
    {
        ProtectionMode.SlideLeft => ProtectionMode.SlideRight,
        ProtectionMode.SlideRight => ProtectionMode.SlideLeft,
        _ => Mode
    }, HelpSlot);
}

/// <summary>Safe releases are evaluated from visible rush movement, never defensive call metadata.</summary>
public sealed record ProtectionRelease(float DecisionTimeout = 2.5f);

public static class PassingGuidance
{
    public static string Reads(ResolvedPlay play)
    {
        if (play.Family != PlayType.Pass || play.Reads.Count == 0) return "";
        string first = play.Reads[0].Slot.GetLabel();
        if (play.Screen != null) return $"Read: {first} | screen";
        var outlet = play.Reads.FirstOrDefault(r => r.Checkdown);
        return $"Read: {first}" + (outlet == null ? "" : $" | Outlet: {outlet.Slot.GetLabel()}");
    }

    public static string Protection(ResolvedPlay play)
    {
        if (play.Screen is { } screen)
            return $"{screen.LineEscorts.Count + screen.SkillEscorts.Count} escort | "
                + (screen.ReleaseSeconds == 0 ? "immediate" : $"release {screen.ReleaseSeconds:0.00}s");
        int count = play.Personnel.LinemanCount + play.Assignments.Count(a => a.Value.Role == AssignmentRole.Block
            || a.Value.ReleaseAfterSeconds > 0);
        string name = play.Protection.Mode switch
        {
            ProtectionMode.SlideLeft => "slide left", ProtectionMode.SlideRight => "slide right",
            ProtectionMode.MaxProtect => "max protect", _ => "balanced"
        };
        return $"{count} protect | {name}";
    }
}

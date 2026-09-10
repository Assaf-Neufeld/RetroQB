using RetroQB.Entities;

namespace RetroQB.Gameplay;

/// <summary>Legacy alignments, preserved independently of play family.</summary>
public static class FormationCatalog
{
    private static readonly Dictionary<FormationType, FormationDefinition> Formations = new()
    {
        [FormationType.BaseTripsRight] = Create(FormationType.BaseTripsRight, new ReceiverPlacement[]
        {
            new(0.12f, 0.3f, ReceiverSlot.WR1),
            new(0.72f, 1.0f, ReceiverSlot.WR2),
            new(0.88f, 0.3f, ReceiverSlot.WR3),
            new(0.38f, 0.05f, ReceiverSlot.TE1),
            new(0.50f, 5.0f, ReceiverSlot.RB1),
        }, ExtraLinemen: 0),

        [FormationType.BaseTripsLeft] = Create(FormationType.BaseTripsLeft, new ReceiverPlacement[]
        {
            new(0.12f, 0.3f, ReceiverSlot.WR1),
            new(0.28f, 1.0f, ReceiverSlot.WR2),
            new(0.88f, 0.3f, ReceiverSlot.WR3),
            new(0.62f, 0.05f, ReceiverSlot.TE1),
            new(0.50f, 5.0f, ReceiverSlot.RB1),
        }, ExtraLinemen: 0),

        [FormationType.BaseSplit] = Create(FormationType.BaseSplit, new ReceiverPlacement[]
        {
            new(0.10f, 0.3f, ReceiverSlot.WR1),
            new(0.26f, 1.0f, ReceiverSlot.WR2),
            new(0.90f, 0.3f, ReceiverSlot.WR3),
            new(0.64f, 0.05f, ReceiverSlot.TE1),
            new(0.50f, 5.0f, ReceiverSlot.RB1),
        }, ExtraLinemen: 0),

        [FormationType.BaseBunchRight] = Create(FormationType.BaseBunchRight, new ReceiverPlacement[]
        {
            new(0.10f, 0.3f, ReceiverSlot.WR1),
            new(0.74f, 0.3f, ReceiverSlot.WR2),
            new(0.80f, 1.2f, ReceiverSlot.WR3),
            new(0.36f, 0.05f, ReceiverSlot.TE1),
            new(0.50f, 5.0f, ReceiverSlot.RB1),
        }, ExtraLinemen: 0),

        [FormationType.BaseBunchLeft] = Create(FormationType.BaseBunchLeft, new ReceiverPlacement[]
        {
            new(0.20f, 1.2f, ReceiverSlot.WR1),
            new(0.26f, 0.3f, ReceiverSlot.WR2),
            new(0.90f, 0.3f, ReceiverSlot.WR3),
            new(0.64f, 0.05f, ReceiverSlot.TE1),
            new(0.50f, 5.0f, ReceiverSlot.RB1),
        }, ExtraLinemen: 0),

        [FormationType.PassSpread] = Create(FormationType.PassSpread, new ReceiverPlacement[]
        {
            new(0.08f, 0.3f, ReceiverSlot.WR1),
            new(0.26f, 1.0f, ReceiverSlot.WR2),
            new(0.74f, 1.0f, ReceiverSlot.WR3),
            new(0.92f, 0.3f, ReceiverSlot.WR4),
            new(0.62f, 0.05f, ReceiverSlot.TE1),
        }, ExtraLinemen: 0),

        [FormationType.PassBunchRight] = Create(FormationType.PassBunchRight, new ReceiverPlacement[]
        {
            new(0.10f, 0.3f, ReceiverSlot.WR1),
            new(0.72f, 0.3f, ReceiverSlot.WR2),
            new(0.78f, 1.2f, ReceiverSlot.WR3),
            new(0.84f, 0.6f, ReceiverSlot.WR4),
            new(0.36f, 0.05f, ReceiverSlot.TE1),
        }, ExtraLinemen: 0),

        [FormationType.PassBunchLeft] = Create(FormationType.PassBunchLeft, new ReceiverPlacement[]
        {
            new(0.16f, 0.6f, ReceiverSlot.WR1),
            new(0.22f, 1.2f, ReceiverSlot.WR2),
            new(0.28f, 0.3f, ReceiverSlot.WR3),
            new(0.90f, 0.3f, ReceiverSlot.WR4),
            new(0.64f, 0.05f, ReceiverSlot.TE1),
        }, ExtraLinemen: 0),

        [FormationType.PassEmpty] = Create(FormationType.PassEmpty, new ReceiverPlacement[]
        {
            new(0.06f, 0.3f, ReceiverSlot.WR1),
            new(0.24f, 1.0f, ReceiverSlot.WR2),
            new(0.76f, 1.0f, ReceiverSlot.WR3),
            new(0.94f, 0.3f, ReceiverSlot.WR4),
            new(0.64f, 0.6f, ReceiverSlot.TE1),
        }, ExtraLinemen: 0),

        [FormationType.RunPowerRight] = Create(FormationType.RunPowerRight, new ReceiverPlacement[]
        {
            new(0.10f, 0.3f, ReceiverSlot.WR1),
            new(0.55f, 4.0f, ReceiverSlot.RB1),
            new(0.70f, 0.05f, ReceiverSlot.TE1),
            new(0.78f, 0.75f, ReceiverSlot.TE2),
        }, ExtraLinemen: 1),

        [FormationType.RunPowerLeft] = Create(FormationType.RunPowerLeft, new ReceiverPlacement[]
        {
            new(0.90f, 0.3f, ReceiverSlot.WR1),
            new(0.45f, 4.0f, ReceiverSlot.RB1),
            new(0.30f, 0.05f, ReceiverSlot.TE1),
            new(0.22f, 0.75f, ReceiverSlot.TE2),
        }, ExtraLinemen: 1),

        [FormationType.RunIForm] = Create(FormationType.RunIForm, new ReceiverPlacement[]
        {
            new(0.12f, 0.3f, ReceiverSlot.WR1),
            new(0.50f, 3.5f, ReceiverSlot.RB1),
            new(0.70f, 0.05f, ReceiverSlot.TE1),
            new(0.78f, 0.75f, ReceiverSlot.TE2),
        }, ExtraLinemen: 1),

        [FormationType.RunSweepRight] = Create(FormationType.RunSweepRight, new ReceiverPlacement[]
        {
            new(0.10f, 0.3f, ReceiverSlot.WR1),
            new(0.60f, 4.6f, ReceiverSlot.RB1),
            new(0.74f, 0.05f, ReceiverSlot.TE1),
            new(0.82f, 0.75f, ReceiverSlot.TE2),
        }, ExtraLinemen: 1),

        [FormationType.RunSweepLeft] = Create(FormationType.RunSweepLeft, new ReceiverPlacement[]
        {
            new(0.90f, 0.3f, ReceiverSlot.WR1),
            new(0.40f, 4.6f, ReceiverSlot.RB1),
            new(0.26f, 0.05f, ReceiverSlot.TE1),
            new(0.18f, 0.75f, ReceiverSlot.TE2),
        }, ExtraLinemen: 1),

        [FormationType.RunStretchRight] = Create(FormationType.RunStretchRight, new ReceiverPlacement[]
        {
            new(0.10f, 0.3f, ReceiverSlot.WR1),
            new(0.56f, 3.2f, ReceiverSlot.RB1),
            new(0.70f, 0.05f, ReceiverSlot.TE1),
            new(0.78f, 0.75f, ReceiverSlot.TE2),
        }, ExtraLinemen: 1),

        [FormationType.RunStretchLeft] = Create(FormationType.RunStretchLeft, new ReceiverPlacement[]
        {
            new(0.90f, 0.3f, ReceiverSlot.WR1),
            new(0.44f, 3.2f, ReceiverSlot.RB1),
            new(0.30f, 0.05f, ReceiverSlot.TE1),
            new(0.22f, 0.75f, ReceiverSlot.TE2),
        }, ExtraLinemen: 1),

        [FormationType.RunPistolStrongRight] = Create(FormationType.RunPistolStrongRight, new ReceiverPlacement[]
        {
            new(0.10f, 0.3f, ReceiverSlot.WR1),
            new(0.50f, 5.1f, ReceiverSlot.RB1),
            new(0.68f, 0.05f, ReceiverSlot.TE1),
            new(0.76f, 0.85f, ReceiverSlot.TE2),
        }, ExtraLinemen: 1),

        [FormationType.RunPistolStrongLeft] = Create(FormationType.RunPistolStrongLeft, new ReceiverPlacement[]
        {
            new(0.90f, 0.3f, ReceiverSlot.WR1),
            new(0.50f, 5.1f, ReceiverSlot.RB1),
            new(0.32f, 0.05f, ReceiverSlot.TE1),
            new(0.24f, 0.85f, ReceiverSlot.TE2),
        }, ExtraLinemen: 1),

        [FormationType.RunSinglebackTripsRight] = Create(FormationType.RunSinglebackTripsRight, new ReceiverPlacement[]
        {
            new(0.10f, 0.3f, ReceiverSlot.WR1),
            new(0.54f, 4.8f, ReceiverSlot.RB1),
            new(0.66f, 1.0f, ReceiverSlot.WR2),
            new(0.86f, 0.3f, ReceiverSlot.WR3),
            new(0.36f, 0.05f, ReceiverSlot.TE1),
        }, ExtraLinemen: 0),

        [FormationType.RunSinglebackTripsLeft] = Create(FormationType.RunSinglebackTripsLeft, new ReceiverPlacement[]
        {
            new(0.90f, 0.3f, ReceiverSlot.WR1),
            new(0.46f, 4.8f, ReceiverSlot.RB1),
            new(0.34f, 1.0f, ReceiverSlot.WR2),
            new(0.14f, 0.3f, ReceiverSlot.WR3),
            new(0.64f, 0.05f, ReceiverSlot.TE1),
        }, ExtraLinemen: 0),
    };

    public static FormationDefinition Get(FormationType type) => Formations.TryGetValue(type, out var formation)
        ? formation : throw new ArgumentException($"Unknown formation: {type}", nameof(type));

    public static IReadOnlyCollection<FormationDefinition> All => Formations.Values;

    private readonly record struct ReceiverPlacement(float XFraction, float YOffset, ReceiverSlot Slot);

    private static FormationDefinition Create(FormationType type, ReceiverPlacement[] placements, int ExtraLinemen)
    {
        float[] lineX = ExtraLinemen == 0
            ? [0.42f, 0.46f, 0.50f, 0.54f, 0.58f]
            : [0.42f, 0.46f, 0.50f, 0.54f, 0.58f, 0.36f];
        // Preserve the old TE separation adjustment in the authored alignment.
        var positions = placements.Select(p => new FormationPoint(
            p.Slot.IsTightEndSlot() ? SeparateTightEnd(p.XFraction, lineX) : p.XFraction, p.YOffset));
        var personnel = ExtraLinemen > 0 ? PersonnelPackages.HeavySixLinemen
            : placements.Any(p => p.Slot == ReceiverSlot.WR4) ? PersonnelPackages.Empty : PersonnelPackages.Eleven;
        return new FormationDefinition(type, personnel, new FormationAlignment(
            new FormationPoint(0.5f, 1.6f), positions, lineX.Select(x => new FormationPoint(x, 0.1f))),
            placements.Select(p => p.Slot));
    }

    private static float SeparateTightEnd(float xFraction, float[] lineX)
    {
        float x = xFraction * Constants.FieldWidth;
        float separation = Constants.ReceiverRadius * 2.2f;
        for (int attempt = 0; attempt < lineX.Length; attempt++)
        {
            var overlaps = lineX.Select(p => p * Constants.FieldWidth)
                .Where(p => MathF.Abs(x - p) < separation).OrderBy(p => MathF.Abs(x - p)).ToArray();
            if (overlaps.Length == 0) break;
            float nearest = overlaps[0];
            x = Math.Clamp(nearest + (x >= nearest ? separation : -separation),
                Constants.ReceiverRadius, Constants.FieldWidth - Constants.ReceiverRadius);
        }
        return x / Constants.FieldWidth;
    }
}


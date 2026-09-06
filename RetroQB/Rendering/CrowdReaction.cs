namespace RetroQB.Rendering;

public static class CrowdReaction
{
    public static float GetSectionPulse(CrowdBackdropState state, float fieldY, bool rightSide)
    {
        if (state.ReactionAge < 0f || state.ReactionStrength <= 0f) return 0f;
        int section = Math.Clamp((int)(fieldY * 4f), 0, 3);
        float center = (section + 0.5f) / 4f;
        float delay = MathF.Abs(center - state.ReactionFieldY) * 1.4f + (rightSide ? 0.12f : 0f);
        float age = state.ReactionAge - delay;
        if (age <= 0f || age >= 1.2f) return 0f;
        return MathF.Sin(age / 1.2f * MathF.PI) * Math.Clamp(state.ReactionStrength, 0f, 1f);
    }
}

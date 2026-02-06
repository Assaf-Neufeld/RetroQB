using System;
using System.Numerics;
using Raylib_cs;

namespace RetroQB.Rendering;

/// <summary>
/// Manages screen-space juice effects: camera shake and full-screen flash.
/// </summary>
public sealed class ScreenEffects
{
    private readonly Random _rng = new();

    // Shake state
    private float _shakeTimer;
    private float _shakeDuration;
    private float _shakeIntensity;
    private Vector2 _shakeOffset;

    // Flash state
    private float _flashTimer;
    private float _flashDuration;
    private float _flashMaxAlpha;
    private Color _flashColor;

    /// <summary>
    /// Current camera offset to apply before drawing the scene.
    /// </summary>
    public Vector2 ShakeOffset => _shakeOffset;

    /// <summary>
    /// Triggers a screen shake effect.
    /// </summary>
    /// <param name="intensity">Max pixel displacement (e.g. 4-6).</param>
    /// <param name="duration">Duration in seconds (e.g. 0.15).</param>
    public void TriggerShake(float intensity = 5f, float duration = 0.15f)
    {
        _shakeIntensity = intensity;
        _shakeDuration = duration;
        _shakeTimer = duration;
    }

    /// <summary>
    /// Triggers a full-screen color flash.
    /// </summary>
    /// <param name="color">Flash color (alpha channel is ignored, use maxAlpha).</param>
    /// <param name="maxAlpha">Peak alpha (0-255). 40-60 for subtle, 100+ for dramatic.</param>
    /// <param name="duration">Duration in seconds (e.g. 0.12).</param>
    public void TriggerFlash(Color color, byte maxAlpha = 50, float duration = 0.12f)
    {
        _flashColor = color;
        _flashMaxAlpha = maxAlpha;
        _flashDuration = duration;
        _flashTimer = duration;
    }

    /// <summary>
    /// Updates timers each frame. Call once per frame.
    /// </summary>
    public void Update(float dt)
    {
        // Update shake
        if (_shakeTimer > 0f)
        {
            _shakeTimer -= dt;
            float t = Math.Clamp(_shakeTimer / _shakeDuration, 0f, 1f);
            float magnitude = _shakeIntensity * t;
            float angle = (float)(_rng.NextDouble() * Math.PI * 2.0);
            _shakeOffset = new Vector2(
                MathF.Cos(angle) * magnitude,
                MathF.Sin(angle) * magnitude);
        }
        else
        {
            _shakeOffset = Vector2.Zero;
        }

        // Update flash
        if (_flashTimer > 0f)
        {
            _flashTimer -= dt;
        }
    }

    /// <summary>
    /// Draws the flash overlay. Call after all scene rendering.
    /// </summary>
    public void DrawFlash()
    {
        if (_flashTimer <= 0f)
        {
            return;
        }

        float t = Math.Clamp(_flashTimer / _flashDuration, 0f, 1f);
        byte alpha = (byte)(_flashMaxAlpha * t);
        Color overlay = new(_flashColor.R, _flashColor.G, _flashColor.B, alpha);

        int screenW = Raylib.GetScreenWidth();
        int screenH = Raylib.GetScreenHeight();
        Raylib.DrawRectangle(0, 0, screenW, screenH, overlay);
    }
}

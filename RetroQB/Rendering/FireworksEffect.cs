using System.Numerics;
using Raylib_cs;
using RetroQB.Core;

namespace RetroQB.Rendering;

public sealed class FireworksEffect
{
    private readonly Random _rng = new();
    private readonly List<Particle> _particles = new();
    private float _activeTimer;
    private float _burstCooldown;

    private struct Particle
    {
        public Vector2 Position;
        public Vector2 Velocity;
        public float Life;
        public float MaxLife;
        public float Size;
        public Color BaseColor;
    }

    public void Trigger(float durationSeconds = 2.4f)
    {
        _activeTimer = MathF.Max(_activeTimer, durationSeconds);
        _burstCooldown = 0f;
        SpawnBurstPair();
    }

    public void Clear()
    {
        _activeTimer = 0f;
        _burstCooldown = 0f;
        _particles.Clear();
    }

    public void Update(float dt)
    {
        if (_activeTimer > 0f)
        {
            _activeTimer -= dt;
        }

        _burstCooldown -= dt;
        if (_activeTimer > 0f && _burstCooldown <= 0f)
        {
            SpawnBurstPair();
            _burstCooldown = 0.18f + (float)_rng.NextDouble() * 0.22f;
        }

        for (int i = _particles.Count - 1; i >= 0; i--)
        {
            Particle p = _particles[i];
            p.Position += p.Velocity * dt;
            p.Velocity += new Vector2(0f, 28f) * dt; // slight gravity
            p.Life -= dt;
            if (p.Life <= 0f)
            {
                _particles.RemoveAt(i);
                continue;
            }
            _particles[i] = p;
        }
    }

    public void Draw()
    {
        foreach (Particle p in _particles)
        {
            float t = Math.Clamp(p.Life / p.MaxLife, 0f, 1f);
            byte alpha = (byte)(t * 255f);
            Color c = new Color(p.BaseColor.R, p.BaseColor.G, p.BaseColor.B, alpha);
            Raylib.DrawCircleV(p.Position, p.Size, c);
        }
    }

    private void SpawnBurstPair()
    {
        Rectangle rect = Constants.FieldRect;
        float left = rect.X + rect.Width * 0.15f;
        float right = rect.X + rect.Width * 0.85f;

        float topMin = rect.Y + rect.Height * 0.05f;
        float topMax = rect.Y + rect.Height * 0.18f;
        float bottomMin = rect.Y + rect.Height * 0.82f;
        float bottomMax = rect.Y + rect.Height * 0.95f;

        Vector2 topBurst = new Vector2(RandomRange(left, right), RandomRange(topMin, topMax));
        Vector2 bottomBurst = new Vector2(RandomRange(left, right), RandomRange(bottomMin, bottomMax));

        SpawnBurst(topBurst);
        SpawnBurst(bottomBurst);
    }

    private void SpawnBurst(Vector2 origin)
    {
        int count = 32 + _rng.Next(12);
        for (int i = 0; i < count; i++)
        {
            float angle = (float)_rng.NextDouble() * MathF.Tau;
            float speed = RandomRange(90f, 170f);
            float life = RandomRange(0.8f, 1.5f);
            float size = RandomRange(2f, 4f);

            _particles.Add(new Particle
            {
                Position = origin,
                Velocity = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * speed,
                Life = life,
                MaxLife = life,
                Size = size,
                BaseColor = PickFireworkColor()
            });
        }
    }

    private Color PickFireworkColor()
    {
        int roll = _rng.Next(6);
        return roll switch
        {
            0 => Palette.Gold,
            1 => Palette.Red,
            2 => Palette.Blue,
            3 => Palette.Orange,
            4 => Palette.Lime,
            _ => Palette.White
        };
    }

    private float RandomRange(float min, float max)
    {
        return min + (float)_rng.NextDouble() * (max - min);
    }
}

using System.Numerics;
using Raylib_cs;
using RetroQB.Core;

namespace RetroQB.Entities;

public enum BallState
{
    HeldByQB,
    HeldByReceiver,
    InAir,
    Dead
}

public sealed class Ball : Entity
{
    public BallState State { get; private set; } = BallState.HeldByQB;
    public Entity? Holder { get; private set; }
    public float AirTime { get; private set; }

    public Ball(Vector2 position) : base(position, Constants.BallRadius, "o", Palette.Gold)
    {
    }

    public void SetHeld(Entity holder, BallState state)
    {
        Holder = holder;
        State = state;
        Velocity = Vector2.Zero;
        AirTime = 0f;
    }

    public void SetInAir(Vector2 position, Vector2 velocity)
    {
        Holder = null;
        State = BallState.InAir;
        Position = position;
        Velocity = velocity;
        AirTime = 0f;
    }

    public void SetDead(Vector2 position)
    {
        Holder = null;
        State = BallState.Dead;
        Position = position;
        Velocity = Vector2.Zero;
    }

    public override void Update(float dt)
    {
        if (State == BallState.HeldByQB || State == BallState.HeldByReceiver)
        {
            if (Holder != null)
            {
                Position = Holder.Position;
            }
            return;
        }

        if (State == BallState.InAir)
        {
            AirTime += dt;
            base.Update(dt);
        }
    }

    public override void Draw()
    {
        Vector2 drawPos = Position;
        
        // When held, offset the ball slightly so it's visible next to the holder
        if ((State == BallState.HeldByQB || State == BallState.HeldByReceiver) && Holder != null)
        {
            drawPos = Holder.Position + new Vector2(0.8f, 0f); // Offset to the right of holder
        }
        
        Vector2 screen = Constants.WorldToScreen(drawPos);
        Raylib.DrawText(Glyph, (int)screen.X - 4, (int)screen.Y - 6, 16, Color);
    }
}

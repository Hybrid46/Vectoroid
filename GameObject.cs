// GameObject.cs
using Raylib_cs;
using System.Numerics;

namespace SpaceShooterMultiplayer
{
    public abstract class GameObject
    {
        public Vector2 Position { get; set; }
        public Color Color { get; set; }
        public int Id { get; set; }

        protected GameObject(Vector2 pos, Color col, int id)
        {
            Position = pos;
            Color = col;
            Id = id;
        }

        public abstract void Update();
        public abstract void Draw();
    }

    public class Player : GameObject
    {
        public float Rotation { get; set; }
        public int Health { get; set; }
        public bool IsThrusting { get; set; }
        private Vector2 velocity = Vector2.Zero;

        public Player(Vector2 pos, float rot, int health, bool thrust, Color col, int id)
            : base(pos, col, id)
        {
            Rotation = rot;
            Health = health;
            IsThrusting = thrust;
        }

        public override void Update()
        {
            if (Raylib.IsKeyDown(KeyboardKey.A)) Rotation -= 4f;
            if (Raylib.IsKeyDown(KeyboardKey.D)) Rotation += 4f;

            IsThrusting = Raylib.IsKeyDown(KeyboardKey.W);
            if (IsThrusting)
            {
                float rad = MathF.PI * Rotation / 180f;
                var dir = new Vector2(MathF.Sin(rad), -MathF.Cos(rad));
                velocity += dir * 0.15f;
            }

            Position += velocity;
            velocity *= 0.98f;

            if (Position.X < -30) Position = new Vector2(1030, Position.Y);
            if (Position.X > 1030) Position = new Vector2(-30, Position.Y);
            if (Position.Y < -30) Position = new Vector2(Position.X, 730);
            if (Position.Y > 730) Position = new Vector2(Position.X, -30);
        }

        public override void Draw()
        {
            float rad = MathF.PI * Rotation / 180f;

            var nose = Position + new Vector2(MathF.Sin(rad), -MathF.Cos(rad)) * 30f;
            var left = Position + new Vector2(MathF.Sin(rad + MathF.PI * 5f / 6f),
                                              -MathF.Cos(rad + MathF.PI * 5f / 6f)) * 20f;
            var right = Position + new Vector2(MathF.Sin(rad - MathF.PI * 5f / 6f),
                                               -MathF.Cos(rad - MathF.PI * 5f / 6f)) * 20f;

            Raylib.DrawTriangle(nose, left, right, Color);

            if (IsThrusting)
            {
                var tail = Position + new Vector2(MathF.Sin(rad + MathF.PI), -MathF.Cos(rad + MathF.PI)) * 20f;
                var flameL = Position + new Vector2(MathF.Sin(rad + MathF.PI + MathF.PI / 6f),
                                                    -MathF.Cos(rad + MathF.PI + MathF.PI / 6f)) * 15f;
                var flameR = Position + new Vector2(MathF.Sin(rad + MathF.PI - MathF.PI / 6f),
                                                    -MathF.Cos(rad + MathF.PI - MathF.PI / 6f)) * 15f;
                Raylib.DrawTriangle(tail, flameL, flameR, Color.Orange);
            }

            Raylib.DrawText($"Player {Id + 1}", (int)Position.X - 30, (int)Position.Y - 50, 18, Color);
        }
    }

    public class Bullet : GameObject
    {
        public Vector2 Velocity { get; set; }

        public Bullet(Vector2 pos, Vector2 vel, Color col, int owner)
            : base(pos, col, owner)
        {
            Velocity = vel;
        }

        public override void Update() => Position += Velocity;
        public override void Draw() => Raylib.DrawCircleV(Position, 4, Color);
    }

    public class Star
    {
        public Vector2 Position { get; set; }
        public float Size { get; set; }

        public Star(Vector2 pos, float size)
        {
            Position = pos;
            Size = size;
        }
    }
}

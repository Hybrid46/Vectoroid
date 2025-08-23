using Raylib_cs;
using System.Numerics;

namespace SpaceShooterMultiplayer
{
    public abstract class GameObject
    {
        public Vector2 Position { get; set; }
        public Color Color { get; set; }
        public int playerId { get; set; }

        protected GameObject(Vector2 pos, Color col, int playerId)
        {
            Position = pos;
            Color = col;
            this.playerId = playerId;
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

            // Calculate forward vector (unit vector) - already in Raylib's coordinate system
            Vector2 forward = new Vector2(MathF.Sin(rad), -MathF.Cos(rad));

            // Calculate nose (30 units in forward direction)
            Vector2 nose = forward * 30f;

            // Calculate left (20 units at 150 degrees from forward)
            Vector2 left = new Vector2(
                forward.X * MathF.Cos(5f * MathF.PI / 6f) - forward.Y * MathF.Sin(5f * MathF.PI / 6f),
                forward.X * MathF.Sin(5f * MathF.PI / 6f) + forward.Y * MathF.Cos(5f * MathF.PI / 6f)
            ) * 20f;

            // Calculate right (20 units at -150 degrees from forward)
            Vector2 right = new Vector2(
                forward.X * MathF.Cos(-5f * MathF.PI / 6f) - forward.Y * MathF.Sin(-5f * MathF.PI / 6f),
                forward.X * MathF.Sin(-5f * MathF.PI / 6f) + forward.Y * MathF.Cos(-5f * MathF.PI / 6f)
            ) * 20f;

            // Convert to absolute coordinates by adding player position
            Vector2 nosePoint = Position + nose;
            Vector2 leftPoint = Position + left;
            Vector2 rightPoint = Position + right;

            // Draw triangle in counter-clockwise order (nose -> right -> left)
            Raylib.DrawTriangle(nosePoint, rightPoint, leftPoint, Color);

            // Thrust effects (flame)
            if (IsThrusting)
            {
                Vector2 backward = -forward; // backward direction

                float angle = MathF.PI / 6f; // 30 degrees

                // Left flame: rotate backward by +30 degrees (counterclockwise)
                Vector2 flameL = new Vector2(
                    backward.X * MathF.Cos(angle) - backward.Y * MathF.Sin(angle),
                    backward.X * MathF.Sin(angle) + backward.Y * MathF.Cos(angle)
                ) * 15f;

                // Right flame: rotate backward by -30 degrees (clockwise)
                Vector2 flameR = new Vector2(
                    backward.X * MathF.Cos(angle) + backward.Y * MathF.Sin(angle),
                    -backward.X * MathF.Sin(angle) + backward.Y * MathF.Cos(angle)
                ) * 15f;

                Vector2 tailPoint = Position + backward * 20f;
                Vector2 flameLPoint = Position + flameL;
                Vector2 flameRPoint = Position + flameR;

                // Draw the flame triangle: tail, right, left (to match the main ship's winding order)
                Raylib.DrawTriangle(tailPoint, flameRPoint, flameLPoint, Color.Orange);
            }
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

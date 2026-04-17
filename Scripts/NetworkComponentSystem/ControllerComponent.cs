using Raylib_cs;
using System.Numerics;

namespace NetworkComponentSystem
{
    // Local component to handle user input and control an entity -> Can be useful to share resources between host/clients for example using PIDs
    public class ControllerComponent : Component, IUpdatable
    {
        public bool IsThrusting { get; set; }
        private Vector2 velocity = Vector2.Zero;
        private float acceleration = 0.15f;
        private float friction = 0.98f;

        public void Update()
        {
            Transform transform = Entity.transform;

            if (Raylib.IsKeyDown(KeyboardKey.A)) transform.Rotation -= 4f;
            if (Raylib.IsKeyDown(KeyboardKey.D)) transform.Rotation += 4f;

            IsThrusting = Raylib.IsKeyDown(KeyboardKey.W);

            if (IsThrusting)
            {
                velocity += transform.forward * acceleration;
            }

            transform.Position += velocity;
            velocity *= friction;

            if (transform.Position.X < -30) transform.Position = new Vector2(1030, transform.Position.Y);
            if (transform.Position.X > 1030) transform.Position = new Vector2(-30, transform.Position.Y);
            if (transform.Position.Y < -30) transform.Position = new Vector2(transform.Position.X, 730);
            if (transform.Position.Y > 730) transform.Position = new Vector2(transform.Position.X, -30);
        }
    }
}

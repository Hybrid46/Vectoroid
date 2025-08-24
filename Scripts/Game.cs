using Raylib_cs;
using System.Numerics;
using System.Collections.Generic;
using NetworkComponentSystem;
using Transform = NetworkComponentSystem.Transform;

namespace SpaceShooterMultiplayer
{
    public class Game
    {
        private enum State { Menu, Playing }
        private State state = State.Menu;

        private readonly NetworkManager net;
        private Entity localPlayer;
        private List<Entity> entities = new List<Entity>();

        private readonly string[] ip = new[] { "127.0.0.1" };
        private int bulletCooldown = 0;

        private readonly List<Star> stars = new List<Star>();
        private readonly Random rnd = new Random();

        public Game(NetworkManager netMgr)
        {
            net = netMgr;
            for (int i = 0; i < 200; i++)
                stars.Add(new Star(
                    new Vector2((float)rnd.NextDouble() * 1000f,
                                (float)rnd.NextDouble() * 700f),
                    (float)rnd.Next(1, 3)));
        }

        public void Update()
        {
            switch (state)
            {
                case State.Menu: UpdateMenu(); break;
                case State.Playing: UpdatePlaying(); break;
            }
        }

        public void Draw()
        {
            Raylib.BeginDrawing();
            Raylib.ClearBackground(Color.Black);

            foreach (var s in stars)
                Raylib.DrawCircleV(s.Position, s.Size, new Color(200, 200, 200, 200));

            switch (state)
            {
                case State.Menu: DrawMenu(); break;
                case State.Playing: DrawPlaying(); break;
            }

            Raylib.EndDrawing();
        }

        /* --------------------------------------------------------------------- */
        /*                            MENU STATE                                 */
        /* --------------------------------------------------------------------- */
        private bool textBoxActive = false;

        private void UpdateMenu()
        {
            // Text‑box focus
            Rectangle boxRect = new Rectangle(400, 250, 200, 30);
            if (Raylib.IsMouseButtonPressed(MouseButton.Left))
                textBoxActive = Raylib.CheckCollisionPointRec(Raylib.GetMousePosition(), boxRect);

            // Edit only when focused
            if (textBoxActive)
            {
                int key = Raylib.GetCharPressed();
                while (key > 0)
                {
                    if (ip[0].Length < 20)
                        ip[0] += (char)key;
                    key = Raylib.GetCharPressed();
                }
                if (Raylib.IsKeyPressed(KeyboardKey.Backspace) && ip[0].Length > 0)
                    ip[0] = ip[0][..^1];
            }

            // Buttons
            Rectangle hostBtn = new Rectangle(400, 300, 200, 40);
            Rectangle joinBtn = new Rectangle(400, 350, 200, 40);
            bool mouseReleased = Raylib.IsMouseButtonReleased(MouseButton.Left);

            bool hostClicked = Raylib.CheckCollisionPointRec(Raylib.GetMousePosition(), hostBtn) && mouseReleased;
            bool joinClicked = Raylib.CheckCollisionPointRec(Raylib.GetMousePosition(), joinBtn) && mouseReleased;

            if (hostClicked)
            {
                net.Host();   // non‑blocking
                int localId = 0;
                CreatePlayer(localId);
                state = State.Playing;
            }
            else if (joinClicked)
            {
                net.Join(ip[0]);   // blocking until connected
                int localId = net.LocalPlayerId;
                CreatePlayer(localId);
                state = State.Playing;
            }
        }

        private void CreatePlayer(int localId)
        {
            Entity e = new Entity();
            e.AddComponent(new Transform());
            e.AddComponent(new HealthComponent(100));
            e.AddComponent(new MovementComponent(Vector2.Zero, 0.1f));
            e.AddComponent(new DrawComponent(0, Color.Green));
            e.AddComponent(new ControllerComponent());

            localPlayer = e;
            entities.Add(e);
        }

        private void DrawMenu()
        {
            Raylib.DrawText("SPACE SHOOTER MULTIPLAYER", 250, 100, 40, Color.White);
            Raylib.DrawText("Host or join a game to play", 320, 160, 20, Color.Gray);

            Raylib.DrawText("IP Address:", 300, 250, 20, Color.White);
            RayGui.GuiTextBox(new Rectangle(400, 250, 200, 30), ip[0], 20, textBoxActive);

            RayGui.GuiButton(new Rectangle(400, 300, 200, 40), "Host Game");
            RayGui.GuiButton(new Rectangle(400, 350, 200, 40), "Join Game");

            Raylib.DrawText($"Status: {net.Status}", 350, 400, 20,
                net.Status.Contains("error") ? Color.Red :
                net.IsConnected ? Color.Green : Color.Yellow);
        }

        /* --------------------------------------------------------------------- */
        /*                            PLAYING STATE                              */
        /* --------------------------------------------------------------------- */
        private void UpdatePlaying()
        {
            // Host may play even with no players connected
            if (!net.IsConnected && !net.IsHost)
            {
                state = State.Menu;
                return;
            }

            HandleEntites();

            if (bulletCooldown > 0) bulletCooldown--;

            if (Raylib.IsKeyPressed(KeyboardKey.Space) && bulletCooldown == 0)
            {
                float rad = MathF.PI * localPlayer.Rotation / 180f;
                var dir = new Vector2(MathF.Sin(rad), -MathF.Cos(rad));

                var pos = localPlayer.Position + dir * 30f;
                var vel = dir * 12f;

                // Create the payload
                var bulletPayload = new
                {
                    type = "Bullet",
                    data = new
                    {
                        OwnerId = localPlayer.playerId,
                        X = pos.X,
                        Y = pos.Y,
                        VX = vel.X,
                        VY = vel.Y
                    }
                };

                // Send to the network
                net.Send(bulletPayload);

                // Add the bullet locally
                net.Bullets.Add(new Bullet(pos, vel, localPlayer.Color, localPlayer.playerId));

                bulletCooldown = 20;
            }

            var statePayload = new
            {
                type = "PlayerState",
                data = new
                {
                    Id = localPlayer.playerId,
                    X = localPlayer.Position.X,
                    Y = localPlayer.Position.Y,
                    Rotation = localPlayer.Rotation,
                    Health = localPlayer.Health,
                    Thrust = localPlayer.IsThrusting
                }
            };
            net.Send(statePayload);
        }

        private void HandleEntites()
        {
            Stack<Entity> entitiesToRemove = new Stack<Entity>();

            foreach (Entity entity in entities)
            {
                entity.Update();
                if (entity.destroy) entitiesToRemove.Push(entity);
            }

            //Remove dead entites
            while (entitiesToRemove.Count > 0)
            {
                Entity entity = entitiesToRemove.Pop();
                entities.Remove(entity);
                Console.WriteLine($"Removed entity: {entity}");
            }
        }

        private void DrawPlaying()
        {
            foreach (Entity entity in entities)
            {
                entity.drawComponent?.Draw();
            }

            Raylib.DrawText($"HEALTH: {localPlayer.healthComponent.CurrentHP}%", 20, 20, 20, Color.Green);
            Raylib.DrawText($"STATUS: {net.Status}", 20, 50, 20, net.IsConnected ? Color.Green : Color.Red);
            Raylib.DrawText("CONTROLS:", 800, 20, 20, Color.White);
            Raylib.DrawText("WASD - Move/Steer", 800, 50, 18, Color.LightGray);
            Raylib.DrawText("SPACE - Shoot", 800, 80, 18, Color.LightGray);
        }
    }
}

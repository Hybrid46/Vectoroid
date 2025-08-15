// Game.cs
using Raylib_cs;
using System.Numerics;
using System.Collections.Generic;

namespace SpaceShooterMultiplayer
{
    public class Game
    {
        private enum State { Menu, Playing }
        private State state = State.Menu;

        private readonly NetworkManager net;
        private Player localPlayer;

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

        /* ------------------------------------------------------------------ */
        /*                           MENU STATE                               */
        /* ------------------------------------------------------------------ */
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
                net.Host();   // blocking – only called once
                localPlayer = new Player(new Vector2(200, 350), 0f, 100, false, Color.Green, 0);
                state = State.Playing;
            }
            else if (joinClicked)
            {
                net.Join(ip[0]);   // blocking – only called once
                localPlayer = new Player(new Vector2(800, 350), 180f, 100, false, Color.Blue, 1);
                state = State.Playing;
            }
        }

        private void DrawMenu()
        {
            Raylib.DrawText("SPACE SHOOTER MULTIPLAYER", 250, 100, 40, Color.White);
            Raylib.DrawText("Host or join a game to play", 320, 160, 20, Color.Gray);

            Raylib.DrawText("IP Address:", 300, 250, 20, Color.White);
            RayGui.GuiTextBox(new Rectangle(400, 250, 200, 30), ip[0], 20, textBoxActive);

            // Visual only
            RayGui.GuiButton(new Rectangle(400, 300, 200, 40), "Host Game");
            RayGui.GuiButton(new Rectangle(400, 350, 200, 40), "Join Game");

            Raylib.DrawText($"Status: {net.Status}", 350, 400, 20,
                net.Status.Contains("error") ? Color.Red :
                net.IsConnected ? Color.Green : Color.Yellow);
        }

        /* ------------------------------------------------------------------ */
        /*                          PLAYING STATE                            */
        /* ------------------------------------------------------------------ */
        private void UpdatePlaying()
        {
            if (!net.IsConnected)
            {
                state = State.Menu;
                return;
            }

            // Update bullets first (remote ones may already be in net.Bullets)
            foreach (var b in net.Bullets)
                b.Update();

            localPlayer.Update();

            if (bulletCooldown > 0) bulletCooldown--;

            if (Raylib.IsKeyPressed(KeyboardKey.Space) && bulletCooldown == 0)
            {
                float rad = MathF.PI * localPlayer.Rotation / 180f;
                var dir = new Vector2(MathF.Sin(rad), -MathF.Cos(rad));

                var pos = localPlayer.Position + dir * 30f;
                var vel = dir * 12f;

                var bulletPayload = new
                {
                    type = "Bullet",
                    data = new
                    {
                        OwnerId = localPlayer.Id,
                        X = pos.X,
                        Y = pos.Y,
                        VX = vel.X,
                        VY = vel.Y
                    }
                };
                net.Send(bulletPayload);

                bulletCooldown = 20;
            }

            var statePayload = new
            {
                type = "PlayerState",
                data = new
                {
                    Id = localPlayer.Id,
                    X = localPlayer.Position.X,
                    Y = localPlayer.Position.Y,
                    Rotation = localPlayer.Rotation,
                    Health = localPlayer.Health,
                    Thrust = localPlayer.IsThrusting
                }
            };
            net.Send(statePayload);
        }

        private void DrawPlaying()
        {
            foreach (var kvp in net.Players)
                kvp.Value.Draw();

            foreach (var b in net.Bullets)
                b.Draw();

            localPlayer.Draw();

            Raylib.DrawText($"HEALTH: {localPlayer.Health}%", 20, 20, 20, Color.Green);
            Raylib.DrawText($"STATUS: {net.Status}", 20, 50, 20,
                net.IsConnected ? Color.Green : Color.Red);

            Raylib.DrawText("CONTROLS:", 800, 20, 20, Color.White);
            Raylib.DrawText("WASD - Move/Steer", 800, 50, 18, Color.LightGray);
            Raylib.DrawText("SPACE - Shoot", 800, 80, 18, Color.LightGray);
        }
    }
}

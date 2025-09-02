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
        int playerId;

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
                playerId = 0;
                CreatePlayer();
                state = State.Playing;
            }
            else if (joinClicked)
            {
                net.Join(ip[0]);   // blocking until connected
                playerId = net.LocalPlayerId;
                CreatePlayer();
                state = State.Playing;
            }
        }

        private void CreatePlayer()
        {
            Entity e = new Entity();
            Transform transform = e.AddComponent(new Transform());
            transform.Position = new Vector2(500, 350);

            e.AddComponent(new HealthComponent(100));
            e.AddComponent(new DrawComponent(0, Color.Green));
            e.AddComponent(new ControllerComponent());

            localPlayer = e;
            entities.Add(e);

            Guid id = Guid.NewGuid();
            NetworkEntity ne = new NetworkEntity(id, playerId, e);
            net.networkEntities.Add(id, ne);

            transform.MarkDirty();
            e.GetComponent<HealthComponent>().MarkDirty();
            e.GetComponent<DrawComponent>().MarkDirty();

            net.SendAddEntity(ne);
        }

        private void CreateBullet()
        {
            Vector2 pos = localPlayer.transform.Position + localPlayer.transform.forward * 30f;
            Vector2 vel = localPlayer.transform.forward * 12f;

            Entity e = new Entity();

            Transform bulletTransform = new Transform();
            bulletTransform.Position = pos;
            bulletTransform.Rotation = localPlayer.transform.Rotation;

            e.AddComponent(bulletTransform);
            e.AddComponent(new BulletHealthComponent(200));
            e.AddComponent(new MovementComponent(bulletTransform.forward, 12f));
            e.AddComponent(new DrawComponent(1, Color.Green));

            entities.Add(e);

            Guid id = Guid.NewGuid();
            NetworkEntity ne = new NetworkEntity(id, playerId, e);
            net.networkEntities.Add(id, ne);

            bulletTransform.MarkDirty();
            e.GetComponent<BulletHealthComponent>().MarkDirty();
            e.GetComponent<MovementComponent>().MarkDirty();
            e.GetComponent<DrawComponent>().MarkDirty();

            net.SendAddEntity(ne);
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
            if (!net.IsConnected && !net.IsHost)
            {
                state = State.Menu;
                return;
            }

            HandleEntites();

            if (bulletCooldown > 0) bulletCooldown--;

            if (Raylib.IsKeyPressed(KeyboardKey.Space) && bulletCooldown == 0)
            {
                CreateBullet();
                bulletCooldown = 20;
            }

            foreach (NetworkEntity ne in net.networkEntities.Values)
            {
                if (net.IsHost)
                {
                    // Host sends all dirty entities
                    if (ne.Local.HasDirtyComponent()) net.SendUpdateEntity(ne);
                }
                else
                {
                    // Client sends updates only for its own player
                    if (ne.playerId == net.LocalPlayerId)  net.SendUpdateEntity(ne);
                }
            }

            // Synchronise local entity list with networkEntities
            // First, remove entities that are no longer in networkEntities
            for (int i = entities.Count - 1; i >= 0; i--)
            {
                Entity entity = entities[i];
                bool found = false;

                foreach (NetworkEntity ne in net.networkEntities.Values)
                {
                    if (ne.Local == entity)
                    {
                        found = true;
                        break;
                    }
                }

                if (!found && entity != localPlayer)
                {
                    entities.RemoveAt(i);
                }
            }

            // Then add new entities
            foreach (NetworkEntity ne in net.networkEntities.Values)
            {
                if (!entities.Contains(ne.Local))
                {
                    entities.Add(ne.Local);
                }
            }
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

                // Find the corresponding NetworkEntity and send a destroy packet
                var ne = net.GetNetworkEntity(entity);
                if (ne != null)
                {
                    net.SendDestroyEntity(ne);
                    net.networkEntities.Remove(ne.id);
                }
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

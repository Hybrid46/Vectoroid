using Raylib_cs;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Net;
using System.Net.Sockets;
using System.Numerics;
using System.Text;
using System.Threading;
using Color = Raylib_cs.Color;
using Rectangle = Raylib_cs.Rectangle;

namespace SpaceShooterMultiplayer
{
    public class Program
    {
        public static void Main()
        {
            Raylib.InitWindow(1000, 700, "Space Shooter Multiplayer");
            Raylib.SetTargetFPS(60);

            NetworkManager networkManager = new NetworkManager();
            Game game = new Game(networkManager);

            while (!Raylib.WindowShouldClose())
            {
                game.Update();
                game.Draw();
            }

            networkManager.Disconnect();
            Raylib.CloseWindow();
        }
    }

    public class NetworkManager
    {
        private Socket socket;
        private Thread receiveThread;
        public bool isHost { get; private set; }
        public bool IsConnected { get; private set; }
        public string Status { get; private set; } = "Disconnected";
        public List<GameObject> GameObjects { get; } = new List<GameObject>();
        private readonly object lockObject = new object();

        public List<GameObject> GetSafeGameObjects()
        {
            lock (lockObject)
            {
                return new List<GameObject>(GameObjects);
            }
        }

        public void HostGame()
        {
            try
            {
                socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                socket.Bind(new IPEndPoint(IPAddress.Any, 12345));
                socket.Listen(1);
                Status = "Waiting for players...";
                isHost = true;

                receiveThread = new Thread(() =>
                {
                    Socket clientSocket = socket.Accept();
                    socket = clientSocket;
                    IsConnected = true;
                    Status = "Player connected!";
                    StartReceiving();
                });
                receiveThread.IsBackground = true;
                receiveThread.Start();
            }
            catch (Exception ex)
            {
                Status = $"Host error: {ex.Message}";
            }
        }

        public void JoinGame(string ipAddress)
        {
            try
            {
                socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                socket.Connect(new IPEndPoint(IPAddress.Parse(ipAddress), 12345));
                IsConnected = true;
                Status = "Connected to host!";
                isHost = false;

                receiveThread = new Thread(StartReceiving);
                receiveThread.IsBackground = true;
                receiveThread.Start();
            }
            catch (Exception ex)
            {
                Status = $"Connection error: {ex.Message}";
            }
        }

        private void StartReceiving()
        {
            try
            {
                byte[] buffer = new byte[4096];
                while (IsConnected)
                {
                    int bytesRead = socket.Receive(buffer);
                    if (bytesRead > 0)
                    {
                        string data = Encoding.ASCII.GetString(buffer, 0, bytesRead);
                        ProcessNetworkData(data);
                    }
                }
            }
            catch (Exception)
            {
                Disconnect();
            }
        }

        private void ProcessNetworkData(string data)
        {
            lock (lockObject)
            {
                string[] objects = data.Split(';');
                foreach (string obj in objects)
                {
                    if (string.IsNullOrEmpty(obj)) continue;

                    string[] parts = obj.Split(':');
                    if (parts[0] == "player")
                    {
                        int id = int.Parse(parts[1]);
                        float x = float.Parse(parts[2]);
                        float y = float.Parse(parts[3]);
                        float rotation = float.Parse(parts[4]);
                        int health = int.Parse(parts[5]);
                        bool thrusting = bool.Parse(parts[6]);

                        // Find existing player or create new
                        Player existing = GameObjects.Find(g => g is Player p && p.Id == id) as Player;

                        if (existing != null)
                        {
                            // Update existing player
                            existing.Position = new Vector2(x, y);
                            existing.Rotation = rotation;
                            existing.Health = health;
                            //TODO existing.IsThrusting = thrusting;
                        }
                        else
                        {
                            // Add new player
                            bool isLocal = (id == 0 && !isHost) || (id == 1 && isHost);
                            Color color = isLocal ? Color.Green : (id == 0 ? Color.Green : Color.Blue);

                            GameObjects.Add(new Player(
                                new Vector2(x, y),
                                rotation,
                                health,
                                thrusting,
                                color,
                                id
                            ));
                        }
                    }
                }
            }
        }

        public void SendData(string data)
        {
            if (IsConnected)
            {
                try
                {
                    byte[] bytes = Encoding.ASCII.GetBytes(data);
                    socket.Send(bytes);
                }
                catch
                {
                    Disconnect();
                }
            }
        }

        public void Disconnect()
        {
            IsConnected = false;
            Status = "Disconnected";
            socket?.Close();
            receiveThread?.Join(100);
        }
    }

    public class Game
    {
        private enum GameState { Menu, Playing }
        private GameState state = GameState.Menu;

        private NetworkManager network;
        private Player localPlayer;
        private string ipInput = "127.0.0.1";
        private int bulletCooldown = 0;
        private List<Star> stars = new List<Star>();
        private Random random = new Random();

        public Game(NetworkManager networkManager)
        {
            network = networkManager;

            // Create background stars
            for (int i = 0; i < 200; i++)
            {
                stars.Add(new Star(
                    new Vector2(random.Next(0, 1000), random.Next(0, 700)),
                    random.Next(1, 3)
                ));
            }
        }

        public void Update()
        {
            switch (state)
            {
                case GameState.Menu:
                    UpdateMenu();
                    break;
                case GameState.Playing:
                    UpdateGame();
                    break;
            }
        }

        private void UpdateMenu()
        {
            // Handle keyboard input for IP address
            int key = Raylib.GetCharPressed();
            while (key > 0)
            {
                if (key >= 32 && key <= 126 && ipInput.Length < 20)
                {
                    ipInput += (char)key;
                }
                key = Raylib.GetCharPressed();
            }

            if (Raylib.IsKeyPressed(KeyboardKey.Backspace) && ipInput.Length > 0)
            {
                ipInput = ipInput[..^1];
            }

            // Host button
            if (RayGui.GuiButton(new Rectangle(400, 300, 200, 40), "Host Game"))
            {
                network.HostGame();
                localPlayer = new Player(new Vector2(200, 350), 0, 100, false, Color.Green, 0);
                state = GameState.Playing;
            }

            // Join button
            if (RayGui.GuiButton(new Rectangle(400, 350, 200, 40), "Join Game"))
            {
                network.JoinGame(ipInput);
                localPlayer = new Player(new Vector2(800, 350), 180, 100, false, Color.Blue, 1);
                state = GameState.Playing;
            }
        }

        private void UpdateGame()
        {
            // Update local player
            localPlayer.Update();

            // Update bullets
            if (bulletCooldown > 0) bulletCooldown--;

            // Shooting
            if (Raylib.IsKeyPressed(KeyboardKey.Space) && bulletCooldown == 0)
            {
                Vector2 direction = new Vector2(
                    (float)Math.Sin(localPlayer.Rotation * Math.PI / 180),
                    -(float)Math.Cos(localPlayer.Rotation * Math.PI / 180)
                );

                Vector2 bulletPos = localPlayer.Position + direction * 30;
                Vector2 bulletVel = direction * 10;

                network.SendData($"bullet:{bulletPos.X}:{bulletPos.Y}:{bulletVel.X}:{bulletVel.Y}:{localPlayer.Id}");
                bulletCooldown = 20;
            }

            // Send player data to host
            if (network.IsConnected)
            {
                network.SendData($"player:{localPlayer.Id}:{localPlayer.Position.X}:{localPlayer.Position.Y}:{localPlayer.Rotation}:{localPlayer.Health}:{localPlayer.IsThrusting}");
            }
            else
            {
                state = GameState.Menu;
            }

            //TODO
            if (network.isHost && network.IsConnected)
            {
                // Host sends its own data to client
                network.SendData($"player:{localPlayer.Id}:{localPlayer.Position.X}:{localPlayer.Position.Y}:{localPlayer.Rotation}:{localPlayer.Health}:{localPlayer.IsThrusting}");
            }
        }

        public void Draw()
        {
            Raylib.BeginDrawing();
            Raylib.ClearBackground(Color.Black);

            // Draw stars
            foreach (Star star in stars)
            {
                Raylib.DrawCircleV(star.Position, star.Size, new Color(200, 200, 200, 200));
            }

            switch (state)
            {
                case GameState.Menu:
                    DrawMenu();
                    break;
                case GameState.Playing:
                    DrawGame();
                    break;
            }

            Raylib.EndDrawing();
        }

        private void DrawMenu()
        {
            Raylib.DrawText("SPACE SHOOTER MULTIPLAYER", 300, 100, 40, Color.White);
            Raylib.DrawText("Host or join a game to play", 350, 150, 20, Color.Gray);

            Raylib.DrawText("IP Address:", 300, 250, 20, Color.White);
            RayGui.GuiTextBox(new Rectangle(400, 250, 200, 30), ipInput, 20, true);

            Raylib.DrawText($"Status: {network.Status}", 400, 400, 20,
                network.Status.Contains("error") ? Color.Red :
                network.IsConnected ? Color.Green : Color.Yellow);
        }

        private void DrawGame()
        {
            // Draw remote objects
            List<GameObject> gameObjects = network.GetSafeGameObjects();

            foreach (GameObject obj in gameObjects)
            {
                obj.Draw();
            }

            // Draw local player
            localPlayer.Draw();

            // Draw HUD
            Raylib.DrawText($"HEALTH: {localPlayer.Health}%", 20, 20, 20, Color.Green);
            Raylib.DrawText($"STATUS: {network.Status}", 20, 50, 20,
                network.IsConnected ? Color.Green : Color.Red);

            Raylib.DrawText("CONTROLS:", 800, 20, 20, Color.White);
            Raylib.DrawText("WASD - Move/Steer", 800, 50, 18, Color.LightGray);
            Raylib.DrawText("SPACE - Shoot", 800, 80, 18, Color.LightGray);
        }
    }

    public abstract class GameObject
    {
        public Vector2 Position { get; set; }
        public Color Color { get; set; }
        public int Id { get; set; }

        public GameObject(Vector2 position, Color color, int id)
        {
            Position = position;
            Color = color;
            Id = id;
        }

        public abstract void Update();
        public abstract void Draw();
    }

    public class Player : GameObject
    {
        public float Rotation { get; set; }
        public int Health { get; set; }
        public bool IsThrusting { get; private set; }
        private Vector2 velocity = Vector2.Zero;

        public Player(Vector2 position, float rotation, int health, bool thrusting, Color color, int id)
            : base(position, color, id)
        {
            Rotation = rotation;
            Health = health;
            IsThrusting = thrusting;
        }

        public override void Update()
        {
            // Rotation
            if (Raylib.IsKeyDown(KeyboardKey.A)) Rotation -= 4f;
            if (Raylib.IsKeyDown(KeyboardKey.D)) Rotation += 4f;

            // Thrust
            IsThrusting = Raylib.IsKeyDown(KeyboardKey.W);
            if (IsThrusting)
            {
                Vector2 direction = new Vector2(
                    (float)Math.Sin(Rotation * Math.PI / 180),
                    -(float)Math.Cos(Rotation * Math.PI / 180)
                );
                velocity += direction * 0.15f;
            }

            // Apply velocity
            Position += velocity;
            velocity *= 0.98f; // Friction

            // Screen wrapping
            if (Position.X < -30) Position = new Vector2(1030, Position.Y);
            if (Position.X > 1030) Position = new Vector2(-30, Position.Y);
            if (Position.Y < -30) Position = new Vector2(Position.X, 730);
            if (Position.Y > 730) Position = new Vector2(Position.X, -30);
        }

        public override void Draw()
        {
            // Draw ship body
            Vector2 nose = Position + new Vector2(
                (float)Math.Sin(Rotation * Math.PI / 180) * 30,
                -(float)Math.Cos(Rotation * Math.PI / 180) * 30
            );

            Vector2 leftWing = Position + new Vector2(
                (float)Math.Sin((Rotation + 150) * Math.PI / 180) * 20,
                -(float)Math.Cos((Rotation + 150) * Math.PI / 180) * 20
            );

            Vector2 rightWing = Position + new Vector2(
                (float)Math.Sin((Rotation - 150) * Math.PI / 180) * 20,
                -(float)Math.Cos((Rotation - 150) * Math.PI / 180) * 20
            );

            Raylib.DrawTriangle(nose, leftWing, rightWing, Color);

            // Draw engine flame when thrusting
            if (IsThrusting)
            {
                Vector2 tail = Position + new Vector2(
                    (float)Math.Sin((Rotation + 180) * Math.PI / 180) * 20,
                    -(float)Math.Cos((Rotation + 180) * Math.PI / 180) * 20
                );

                Vector2 flameLeft = Position + new Vector2(
                    (float)Math.Sin((Rotation + 210) * Math.PI / 180) * 15,
                    -(float)Math.Cos((Rotation + 210) * Math.PI / 180) * 15
                );

                Vector2 flameRight = Position + new Vector2(
                    (float)Math.Sin((Rotation + 150) * Math.PI / 180) * 15,
                    -(float)Math.Cos((Rotation + 150) * Math.PI / 180) * 15
                );

                Raylib.DrawTriangle(tail, flameLeft, flameRight, Color.Orange);
            }

            // Draw player ID
            Raylib.DrawText($"Player {Id + 1}", (int)Position.X - 30, (int)Position.Y - 50, 18, Color);
        }
    }

    public class Bullet : GameObject
    {
        public Vector2 Velocity { get; set; }

        public Bullet(Vector2 position, Vector2 velocity, Color color, int ownerId)
            : base(position, color, ownerId)
        {
            Velocity = velocity;
        }

        public override void Update()
        {
            Position += Velocity;
        }

        public override void Draw()
        {
            Raylib.DrawCircleV(Position, 4, Color);
        }
    }

    public class Star
    {
        public Vector2 Position { get; set; }
        public float Size { get; set; }

        public Star(Vector2 position, float size)
        {
            Position = position;
            Size = size;
        }
    }

    // Simple GUI elements since Raylib-cs doesn't include a GUI library
    public static class RayGui
    {
        public static bool GuiButton(Rectangle bounds, string text)
        {
            Color baseColor = new Color(50, 100, 150, 255);
            Color hoverColor = new Color(70, 120, 180, 255);
            Color pressColor = new Color(30, 80, 130, 255);

            bool isHovered = Raylib.CheckCollisionPointRec(Raylib.GetMousePosition(), bounds);
            bool isPressed = isHovered && Raylib.IsMouseButtonDown(MouseButton.Left);
            bool clicked = isHovered && Raylib.IsMouseButtonReleased(MouseButton.Left);

            Color color = isPressed ? pressColor : (isHovered ? hoverColor : baseColor);

            Raylib.DrawRectangleRec(bounds, color);
            Raylib.DrawRectangleLinesEx(bounds, 2, Color.White);
            Raylib.DrawText(text,
                (int)(bounds.X + bounds.Width / 2 - Raylib.MeasureText(text, 20) / 2),
                (int)(bounds.Y + bounds.Height / 2 - 10),
                20, Color.White);

            return clicked;
        }

        public static string GuiTextBox(Rectangle bounds, string text, int maxLength, bool editMode)
        {
            Color baseColor = new Color(30, 30, 30, 255);
            Color activeColor = new Color(40, 40, 40, 255);

            bool isActive = editMode;
            Color color = isActive ? activeColor : baseColor;

            Raylib.DrawRectangleRec(bounds, color);
            Raylib.DrawRectangleLinesEx(bounds, 2, Color.White);
            Raylib.DrawText(text, (int)bounds.X + 5, (int)bounds.Y + 5, 20, Color.White);

            // Draw cursor when active
            if (isActive && (Raylib.GetTime() * 2) % 2 < 1)
            {
                Raylib.DrawLine(
                    (int)(bounds.X + 5 + Raylib.MeasureText(text, 20)),
                    (int)(bounds.Y + 5),
                    (int)(bounds.X + 5 + Raylib.MeasureText(text, 20)),
                    (int)(bounds.Y + bounds.Height - 5),
                    Color.White
                );
            }

            return text;
        }
    }
}
// NetworkManager.cs
using Raylib_cs;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Numerics;
using System.Text;
using System.Text.Json;

namespace SpaceShooterMultiplayer
{
    public class NetworkManager
    {
        private TcpClient client;
        private TcpListener listener;
        private NetworkStream stream;

        // ---------- Public state ----------
        public bool IsHost { get; private set; }
        public bool IsConnected => stream != null && stream.CanRead && stream.CanWrite;
        public string Status { get; private set; } = "Disconnected";

        // ---------- Shared data ----------
        private readonly Dictionary<int, Player> players = new Dictionary<int, Player>();
        private readonly List<Bullet> bullets = new List<Bullet>();

        public IReadOnlyDictionary<int, Player> Players => players;
        public List<Bullet> Bullets => bullets;

        // ---------- Networking ----------
        public void Host()
        {
            try
            {
                listener = new TcpListener(IPAddress.Any, 12345);
                listener.Start(1);
                Status = "Waiting for a player...";
                Console.WriteLine("Hosting game...Waiting for a player...");
                IsHost = true;

                client = listener.AcceptTcpClient(); // blocking
                stream = client.GetStream();
                Status = "Player connected!";
            }
            catch (Exception ex)
            {
                Status = $"Host error: {ex.Message}";
            }
        }

        public void Join(string ip)
        {
            try
            {
                client = new TcpClient();
                client.Connect(IPAddress.Parse(ip), 12345); // blocking
                stream = client.GetStream();
                Status = "Connected to host!";
                IsHost = false;
            }
            catch (Exception ex)
            {
                Status = $"Connect error: {ex.Message}";
            }
        }

        public void Disconnect()
        {
            try
            {
                stream?.Close();
                client?.Close();
                listener?.Stop();
                Status = "Disconnected";
            }
            catch { }
        }

        public void Send(object payload)
        {
            if (!IsConnected) return;
            try
            {
                string json = JsonSerializer.Serialize(payload);
                byte[] data = Encoding.UTF8.GetBytes(json + "\n");
                stream.Write(data, 0, data.Length);
            }
            catch
            {
                Disconnect();
            }
        }

        // ---------- Non‑blocking receive ----------
        private string _inBuffer = "";

        public void Poll()
        {
            if (!IsConnected) return;

            while (stream.DataAvailable)
            {
                byte[] buffer = new byte[4096];
                int read = stream.Read(buffer, 0, buffer.Length);
                if (read == 0)   // remote closed
                {
                    Disconnect();
                    return;
                }
                _inBuffer += Encoding.UTF8.GetString(buffer, 0, read);

                // split on '\n'
                int idx;
                while ((idx = _inBuffer.IndexOf('\n')) != -1)
                {
                    string line = _inBuffer.Substring(0, idx);
                    if (!string.IsNullOrWhiteSpace(line))
                        ProcessMessage(line.Trim());
                    _inBuffer = _inBuffer.Substring(idx + 1);
                }
            }
        }

        private void ProcessMessage(string json)
        {
            try
            {
                var doc = JsonDocument.Parse(json);
                string type = doc.RootElement.GetProperty("type").GetString();
                JsonElement data = doc.RootElement.GetProperty("data");

                if (type == "PlayerState")
                {
                    var p = JsonSerializer.Deserialize<PlayerStateDto>(data.GetRawText());
                    if (!players.TryGetValue(p.Id, out var existing))
                    {
                        existing = new Player(new Vector2(0, 0), 0f, 100, false, Color.Blue, p.Id);
                        players[p.Id] = existing;
                    }
                    existing.Position = new Vector2(p.X, p.Y);
                    existing.Rotation = p.Rotation;
                    existing.Health = p.Health;
                    existing.IsThrusting = p.Thrust;
                }
                else if (type == "Bullet")
                {
                    var b = JsonSerializer.Deserialize<BulletDto>(data.GetRawText());
                    bullets.Add(new Bullet(
                        new Vector2(b.X, b.Y),
                        new Vector2(b.VX, b.VY),
                        Color.Red,
                        b.OwnerId));
                }
            }
            catch
            {
                // ignore malformed packet
            }
        }

        // ---------- DTOs ----------
        private record PlayerStateDto
        {
            public int Id { get; init; }
            public float X { get; init; }
            public float Y { get; init; }
            public float Rotation { get; init; }
            public int Health { get; init; }
            public bool Thrust { get; init; }
        }

        private record BulletDto
        {
            public int OwnerId { get; init; }
            public float X { get; init; }
            public float Y { get; init; }
            public float VX { get; init; }
            public float VY { get; init; }
        }
    }
}

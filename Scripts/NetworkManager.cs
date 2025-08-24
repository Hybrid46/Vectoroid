using NetworkComponentSystem;
using Raylib_cs;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Numerics;
using System.Text;
using System.Text.Json;
using Transform = NetworkComponentSystem.Transform;

namespace SpaceShooterMultiplayer
{
    public class NetworkManager
    {
        private TcpClient client;            // used only by a connecting client
        private TcpListener listener;        // only used by the host
        private NetworkStream stream;        // stream for a connecting client

        // ---------- Public state ----------
        public bool IsHost { get; private set; }
        public bool IsConnected => IsHost || (stream != null && stream.CanRead && stream.CanWrite);
        public string Status { get; private set; } = "Disconnected";

        public int LocalPlayerId { get; private set; } = -1; // 0 for host, otherwise unique hash

        // ---------- Shared data ----------
        private readonly Dictionary<int, Player> players = new Dictionary<int, Player>();
        private readonly List<Bullet> bullets = new List<Bullet>();

        Dictionary<int, NetworkEntity> networkEntities = new Dictionary<int, NetworkEntity>();

        public IReadOnlyDictionary<int, Player> Players => players;
        public List<Bullet> Bullets => bullets;

        // ---------- Client connections (for host) ----------
        private readonly List<TcpClient> clientConnections = new List<TcpClient>();
        private readonly Dictionary<TcpClient, string> clientBuffers = new Dictionary<TcpClient, string>();

        // ---------- Local buffer for a connected client ----------
        private string clientBuffer = "";

        /* ------------------------------------------------------------------ */
        /*                               HOST                                 */
        /* ------------------------------------------------------------------ */
        public void Host()
        {
            try
            {
                listener = new TcpListener(IPAddress.Any, 12345);
                listener.Start(1);                // allow one pending connection
                Status = "Hosting…";
                IsHost = true;
                LocalPlayerId = 0;                // host id
            }
            catch (Exception ex)
            {
                Status = $"Host error: {ex.Message}";
            }
        }

        /* ------------------------------------------------------------------ */
        /*                               JOIN                                 */
        /* ------------------------------------------------------------------ */
        public void Join(string ip)
        {
            try
            {
                client = new TcpClient();
                client.Connect(IPAddress.Parse(ip), 12345); // blocking until success
                stream = client.GetStream();
                Status = "Connected to host!";
                LocalPlayerId = client.Client.RemoteEndPoint.GetHashCode();
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
                foreach (var c in clientConnections) c?.Close();
                listener?.Stop();
                Status = "Disconnected";
            }
            catch { }
        }

        /* ------------------------------------------------------------------ */
        /*                               SEND                                 */
        /* ------------------------------------------------------------------ */
        public void Send(object payload)
        {
            if (!IsConnected && !IsHost) return;
            try
            {
                string json = JsonSerializer.Serialize(payload);
                if (IsHost)
                {
                    Broadcast(json + "\n");
                }
                else
                {
                    byte[] data = Encoding.UTF8.GetBytes(json + "\n");
                    stream.Write(data, 0, data.Length);
                }
            }
            catch
            {
                Disconnect();
            }
        }

        /* ------------------------------------------------------------------ */
        /*                              POLL                                  */
        /* ------------------------------------------------------------------ */
        public void Poll()
        {
            if (!IsConnected && !IsHost) return;

            // ---- Host: accept new connections --------------------------------
            if (IsHost)
            {
                while (listener.Pending())
                {
                    TcpClient newClient = listener.AcceptTcpClient();
                    clientConnections.Add(newClient);
                    clientBuffers[newClient] = "";
                    // Create a placeholder player for the new connection
                    int clientId = newClient.Client.RemoteEndPoint.GetHashCode();
                    if (!players.ContainsKey(clientId))
                    {
                        players[clientId] = new Player(new Vector2(0, 0), 0f, 100, false, Color.Blue, clientId);

                        Entity e = new Entity();
                        e.AddComponent(new Transform());
                        e.AddComponent(new HealthComponent(100));
                        e.AddComponent(new MovementComponent(Vector2.Zero, 0.1f));
                        e.AddComponent(new ColorComponent(Color.Blue));

                        NetworkEntity networkEntity = new NetworkEntity(clientId, e);

                        networkEntities.Add(clientId, networkEntity);
                    }
                }
            }

            // ---- Read from all streams --------------------------------------
            if (IsHost)
            {
                // read from each connected client
                for (int i = clientConnections.Count - 1; i >= 0; i--)
                {
                    TcpClient c = clientConnections[i];
                    NetworkStream ns = c.GetStream();
                    if (!ns.DataAvailable) continue;

                    byte[] buffer = new byte[4096];
                    int read = ns.Read(buffer, 0, buffer.Length);
                    if (read == 0)
                    {
                        // client closed
                        c.Close();
                        clientConnections.RemoveAt(i);
                        clientBuffers.Remove(c);
                        continue;
                    }

                    string chunk = Encoding.UTF8.GetString(buffer, 0, read);
                    clientBuffers[c] += chunk;

                    ProcessBuffer(c, clientBuffers[c], true);
                }
            }
            else
            {
                // client side – single stream
                if (stream.DataAvailable)
                {
                    byte[] buffer = new byte[4096];
                    int read = stream.Read(buffer, 0, buffer.Length);
                    if (read == 0)
                    {
                        Disconnect();
                        return;
                    }
                    string chunk = Encoding.UTF8.GetString(buffer, 0, read);
                    clientBuffer += chunk;
                    ProcessBuffer(null, clientBuffer, false);
                }
            }
        }

        /* ------------------------------------------------------------------ */
        /*                          PROCESS BUFFER                           */
        /* ------------------------------------------------------------------ */
        private void ProcessBuffer(TcpClient? sender, string buffer, bool isHost)
        {
            int idx;
            while ((idx = buffer.IndexOf('\n')) != -1)
            {
                string line = buffer.Substring(0, idx).Trim();
                if (!string.IsNullOrWhiteSpace(line))
                {
                    ProcessMessage(line, sender);
                }
                buffer = buffer.Substring(idx + 1);
            }

            if (sender != null) clientBuffers[sender] = buffer;
            else clientBuffer = buffer;
        }

        /* ------------------------------------------------------------------ */
        /*                           MESSAGE PROCESSING                       */
        /* ------------------------------------------------------------------ */
        private void ProcessMessage(string json, TcpClient? sender)
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

                // Forward the message to all other participants (if we are the host)
                if (IsHost && sender != null)
                {
                    Broadcast(json + "\n", sender);
                }
            }
            catch
            {
                // ignore malformed packets
            }
        }

        /* ------------------------------------------------------------------ */
        /*                                 BROADCAST                           */
        /* ------------------------------------------------------------------ */
        private void Broadcast(string data, TcpClient? except = null)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(data);
            for (int i = clientConnections.Count - 1; i >= 0; i--)
            {
                TcpClient c = clientConnections[i];
                if (except != null && c == except) continue;
                try
                {
                    NetworkStream ns = c.GetStream();
                    ns.Write(bytes, 0, bytes.Length);
                }
                catch
                {
                    c.Close();
                    clientConnections.RemoveAt(i);
                    clientBuffers.Remove(c);
                }
            }
        }
    }
}

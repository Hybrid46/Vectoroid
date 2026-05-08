// ─────────────────────────────────────────────────────────────────────
// NetworkManager.cs – binary‑serialization networking
// ─────────────────────────────────────────────────────────────────────
using NetworkComponentSystem;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;          // for optional debug logging
using Transform = NetworkComponentSystem.Transform;

namespace SpaceShooterMultiplayer
{
    public class NetworkManager
    {
        // ──────────────────────────────────────────────────────────────────────
        //                          EVENTS
        // ──────────────────────────────────────────────────────────────────────
        public event Action ServerStarted;
        public event Action ServerStopped;
        public event Action<int> ClientConnected;    // (host) new client joined
        public event Action<int> ClientDisconnected; // (host) client left
        public event Action<int> PlayerIdAssigned;   // (client) received ID from host

        private bool _idReceived = false; // Prevent duplicate triggers

        private TcpClient client;            // used only by a connecting client
        private TcpListener listener;        // only used by the host
        private NetworkStream stream;        // stream for a connecting client

        // ---------- Public state ----------
        public bool IsHost { get; private set; }
        public bool IsConnected => IsHost || (stream != null && stream.CanRead && stream.CanWrite);
        public string Status { get; private set; } = "Disconnected";
        public int LocalPlayerId { get; private set; } = -1; // 0 for host

        // ---------- Shared data ----------
        public Dictionary<Guid, NetworkEntity> networkEntities = new Dictionary<Guid, NetworkEntity>();

        public Entity playerEntity; // The local player's entity

        // ---------- Client connections (for host) ----------
        private readonly List<TcpClient> clientConnections = new List<TcpClient>();
        //   buffer that still contains incomplete data for every client
        private readonly Dictionary<TcpClient, byte[]> clientBuffers = new Dictionary<TcpClient, byte[]>();

        // ---------- Local buffer for a connected client ----------
        private byte[] clientBuffer = Array.Empty<byte>();

        int nextPlayerId = 1;

        /* ──────────────────────────────────────────────────────────────────────
         *                               HOST
         * ────────────────────────────────────────────────────────────────────── */
        public void Host()
        {
            try
            {
                listener = new TcpListener(IPAddress.Any, 12345);
                listener.Start(1);                // allow one pending connection
                Status = "Hosting…";
                IsHost = true;
                LocalPlayerId = 0;                // host id
                ServerStarted?.Invoke();
            }
            catch (Exception ex)
            {
                Status = $"Host error: {ex.Message}";
            }
        }

        /* ──────────────────────────────────────────────────────────────────────
         *                               JOIN
         * ────────────────────────────────────────────────────────────────────── */
        public void Join(string ip)
        {
            try
            {
                client = new TcpClient();
                client.Connect(IPAddress.Parse(ip), 12345); // blocking until success
                stream = client.GetStream();
                Status = "Connected to host!";
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

                //TODO maybe later we will handle conenctions better and receive playerIDs, now lets use client indexes
                int playerID = 0;
                foreach (var c in clientConnections)
                {
                    ClientDisconnected?.Invoke(playerID);
                    c?.Close();
                    playerID++;
                }
                listener?.Stop();
                Status = "Disconnected";
                ServerStopped?.Invoke();
            }
            catch { }
        }

        /* ──────────────────────────────────────────────────────────────────────
         *                               SEND
         * ────────────────────────────────────────────────────────────────────── */
        /// <summary>
        /// Sends a *length‑prefixed* binary packet.
        /// The packet is first wrapped as [int32 length][payload].
        /// </summary>
        public void SendRaw(byte[] payload)
        {
            if (!IsConnected && !IsHost) return;

            // prepend packet length
            var packet = new byte[4 + payload.Length];
            Buffer.BlockCopy(BitConverter.GetBytes(payload.Length), 0, packet, 0, 4);
            Buffer.BlockCopy(payload, 0, packet, 4, payload.Length);

            try
            {
                if (IsHost)
                {
                    // broadcast to all connected clients
                    Broadcast(packet);
                }
                else
                {
                    // client → server
                    stream.Write(packet, 0, packet.Length);
                }
            }
            catch
            {
                Disconnect();
            }
        }

        private void SendRawToClient(TcpClient target, byte[] payload)
        {
            var packet = new byte[4 + payload.Length];
            Buffer.BlockCopy(BitConverter.GetBytes(payload.Length), 0, packet, 0, 4);
            Buffer.BlockCopy(payload, 0, packet, 4, payload.Length);

            try
            {
                NetworkStream ns = target.GetStream();
                ns.Write(packet, 0, packet.Length);
            }
            catch
            {
                // If this specific client fails, remove it
                target.Close();
                clientConnections.Remove(target);
                clientBuffers.Remove(target);
            }
        }

        /* ──────────────────────────────────────────────────────────────────────
         *                               POLL
         * ────────────────────────────────────────────────────────────────────── */
        public void Poll()
        {
            if (!IsConnected && !IsHost) return;

            /* ---- Host: accept new connections ---- */
            if (IsHost)
            {
                while (listener.Pending())
                {
                    TcpClient newClient = listener.AcceptTcpClient();
                    clientConnections.Add(newClient);
                    clientBuffers[newClient] = Array.Empty<byte>();

                    int assignedId = nextPlayerId++;

                    // Send ID to client immediately
                    using MemoryStream ms = new MemoryStream();
                    using BinaryWriter bw = new BinaryWriter(ms);
                    bw.Write((byte)MessageType.AssignPlayerId);
                    bw.Write(assignedId);
                    Console.WriteLine($"Player ID [{assignedId}] sent");
                    SendRawToClient(newClient, ms.ToArray());

                    ClientConnected?.Invoke(assignedId);

                    SendExistingEntitiesToClient(newClient);
                }
            }

            /* ---- Read from all streams ---- */
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

                    // Append the newly read bytes to the client's buffer
                    var old = clientBuffers[c];
                    var newBuf = new byte[old.Length + read];
                    Buffer.BlockCopy(old, 0, newBuf, 0, old.Length);
                    Buffer.BlockCopy(buffer, 0, newBuf, old.Length, read);
                    clientBuffers[c] = newBuf;

                    // process all complete packets in the buffer
                    var b = clientBuffers[c];
                    ProcessBuffer(c, ref b, true);
                    clientBuffers[c] = b;
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

                    // Append the newly read bytes to the local buffer
                    var old = clientBuffer;
                    var newBuf = new byte[old.Length + read];
                    Buffer.BlockCopy(old, 0, newBuf, 0, old.Length);
                    Buffer.BlockCopy(buffer, 0, newBuf, old.Length, read);
                    clientBuffer = newBuf;

                    // process all complete packets
                    ProcessBuffer(null, ref clientBuffer, false);
                }
            }
        }

        /* ──────────────────────────────────────────────────────────────────────
         *                          PROCESS BUFFER
         * ────────────────────────────────────────────────────────────────────── */
        /// <summary>
        /// Extracts full packets from the supplied byte array.
        /// Packets are encoded as [int32 length][payload].
        /// For each complete packet, <see cref="ProcessMessage"/> is invoked.
        /// The remaining incomplete bytes (if any) are left in <paramref name="buffer"/>.
        /// </summary>
        private void ProcessBuffer(TcpClient? sender, ref byte[] buffer, bool isHost)
        {
            while (buffer.Length >= 4)
            {
                int packetLen = BitConverter.ToInt32(buffer, 0);
                if (packetLen <= 0 || buffer.Length < 4 + packetLen) break;

                // slice payload
                var payload = new byte[packetLen];
                Buffer.BlockCopy(buffer, 4, payload, 0, packetLen);

                // remove the processed packet from the buffer
                int remaining = buffer.Length - (4 + packetLen);
                if (remaining > 0)
                {
                    var rest = new byte[remaining];
                    Buffer.BlockCopy(buffer, 4 + packetLen, rest, 0, remaining);
                    buffer = rest;
                }
                else
                {
                    buffer = Array.Empty<byte>();
                }

                // dispatch
                ProcessMessage(payload, sender);
            }
        }

        /* ──────────────────────────────────────────────────────────────────────
         *                           MESSAGE PROCESSING
         * ────────────────────────────────────────────────────────────────────── */
        private void ProcessMessage(byte[] data, TcpClient? sender)
        {
            // Assign Pplayer ID
            using MemoryStream ms = new MemoryStream(data);
            using BinaryReader br = new BinaryReader(ms);

            byte msgType = br.ReadByte();

            if (msgType == 3) // AssignPlayerId
            {
                LocalPlayerId = br.ReadInt32();
                Console.WriteLine($"Player ID [{LocalPlayerId}] assigned");
                
                if (!_idReceived)
                {
                    _idReceived = true;
                    PlayerIdAssigned?.Invoke(LocalPlayerId);
                }

                return;
            }

            // 1. If we are the host, broadcast the packet to all
            //    other clients (except the sender).  The packet is
            //    unchanged – it already contains the message type
            //    (Add / Update / Destroy) and the payload.
            if (IsHost && sender != null)
            {
                BroadcastPayload(data, sender);
            }

            // 2. Apply the packet to our local entity store.
            //    NetworkEntity.ProcessEntity will decode the packet,
            //    update or create the entity, and reset dirty flags.
            NetworkEntity.ProcessEntity(data, networkEntities);
        }

        /* ──────────────────────────────────────────────────────────────────────
         *                               BROADCAST
         * ────────────────────────────────────────────────────────────────────── */
        /// <summary>
        /// Sends <paramref name="data"/> to every connected client except
        /// <paramref name="except"/> (if non‑null).
        /// </summary>
        private void Broadcast(byte[] data, TcpClient? except = null)
        {
            for (int i = clientConnections.Count - 1; i >= 0; i--)
            {
                TcpClient c = clientConnections[i];
                if (except != null && c == except) continue;

                try
                {
                    NetworkStream ns = c.GetStream();
                    ns.Write(data, 0, data.Length);
                }
                catch
                {
                    // socket error – drop the client
                    c.Close();
                    clientConnections.RemoveAt(i);
                    clientBuffers.Remove(c);
                }
            }
        }

        private void BroadcastPayload(byte[] payload, TcpClient? except = null)
        {
            var packet = new byte[4 + payload.Length];
            Buffer.BlockCopy(BitConverter.GetBytes(payload.Length), 0, packet, 0, 4);
            Buffer.BlockCopy(payload, 0, packet, 4, payload.Length);
            Broadcast(packet, except);
        }

        /* ──────────────────────────────────────────────────────────────────────
         *  Helpers for sending specific entity messages
         * ────────────────────────────────────────────────────────────────────── */
        /// <summary>
        /// Sends a full Add message for <paramref name="ne"/>.
        /// </summary>
        public void SendAddEntity(NetworkEntity ne)
        {
            byte[] payload = NetworkEntity.EncodeEntity(ne, 0); // MessageType 0 = Add
            SendRaw(payload);
        }

        /// <summary>
        /// Sends a Destroy message for <paramref name="ne"/>.
        /// </summary>
        public void SendDestroyEntity(NetworkEntity ne)
        {
            // Build a packet: [byte msgType][int32 playerId][int32 guid parts][uint32 mask (0)]
            using MemoryStream ms = new MemoryStream();
            using BinaryWriter bw = new BinaryWriter(ms);
            bw.Write((byte)MessageType.Destroy);
            bw.Write(ne.playerId);
            var (a, b, c, d) = GuidPacker.PackGuid(ne.id);
            bw.Write(a); bw.Write(b); bw.Write(c); bw.Write(d);
            bw.Write((uint)0);                                // mask = 0
            SendRaw(ms.ToArray());
        }

        /// <summary>
        /// Sends an Update message for <paramref name="ne"/>.  Only dirty components
        /// will be encoded (handled inside EncodeEntity).
        /// </summary>
        public void SendUpdateEntity(NetworkEntity ne)
        {
            uint mask = ne.Local.GetDirtyNetworkMask();

            // Skip if nothing dirty
            if (mask == 0) return;

            byte[] payload = NetworkEntity.EncodeEntity(ne, MessageType.Update);
            SendRaw(payload);
        }

        private void SendExistingEntitiesToClient(TcpClient client)
        {
            foreach (var ne in networkEntities.Values)
            {
                byte[] payload = NetworkEntity.EncodeEntity(ne, MessageType.Add);
                SendRawToClient(client, payload);
            }
        }


        /// <summary>Returns the NetworkEntity that owns <paramref name="entity"/> or null.</summary>
        public NetworkEntity? GetNetworkEntity(Entity entity)
        {
            foreach (var kv in networkEntities)
            {
                if (kv.Value.Local == entity) return kv.Value;
            }
            return null;
        }
    }
}

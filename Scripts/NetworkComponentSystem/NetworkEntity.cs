// ─────────────────────────────────────────────────────────────────────
// NetworkEntity.cs – packet handling
// ─────────────────────────────────────────────────────────────────────
using System.Numerics;
using Raylib_cs;
using static NetworkComponentSystem.Component;

namespace NetworkComponentSystem
{
    public enum MessageType : byte
    {
        Add = 0,
        Destroy = 1,
        Update = 2,
        AssignPlayerId = 3
    }

    public class NetworkEntity
    {
        // unique per session
        public Guid id;
        // which player owns this entity
        public int playerId;
        // the *real* entity (server) or the ghost (client)
        public Entity Local;

        public NetworkEntity(Guid id, int playerId, Entity local)
        {
            this.id = id;
            this.playerId = playerId;
            Local = local;
        }

        //Packet layout -> [MessageType][PlayerId][EntityId][ComponentMask][Payload]
        internal static byte[] EncodeEntity(NetworkEntity ne, MessageType MessageType)
        {
            using MemoryStream ms = new MemoryStream();
            using BinaryWriter bw = new BinaryWriter(ms);

            bw.Write((byte)MessageType);             // MessageType
            bw.Write(ne.playerId);                   // PlayerId

            var (a, b, c, d) = GuidPacker.PackGuid(ne.id); // EntityId
            bw.Write(a);
            bw.Write(b);
            bw.Write(c);
            bw.Write(d);

            // For Add‑messages we always include all components
            uint mask = 0u;

            if (MessageType == MessageType.Add)
            {
                mask = ne.Local.GetFullNetworkMask();
            }
            else                            // Update / Destroy
            {
                mask = ne.Local.GetDirtyNetworkMask();
            }
            // ------------------------------------------------------------------

            bw.Write(mask);

            // Encode each dirty component
            for (byte i = 0; i <= 5; i++)
            {
                var type = (ComponentType)i;
                if ((mask & (uint)(1 << i)) == 0) continue;

                var comp = ne.Local.GetComponentByType(type);
                if (comp is INetworkComponent networkComp)
                {
                    networkComp.Encode(bw);
                }
            }

            // Reset dirty flags only for Update packets (Add packets are already clean)
            if (MessageType != MessageType.Add)
            {
                ne.Local.ResetDirtyFlags(mask);
            }

            return ms.ToArray();
        }


        public static void ProcessEntity(byte[] data, Dictionary<Guid, NetworkEntity> networkEntities)
        {
            using MemoryStream ms = new MemoryStream(data);
            using BinaryReader br = new BinaryReader(ms);

            MessageType msgType = (MessageType)br.ReadByte();
            int playerId = br.ReadInt32();

            int a = br.ReadInt32();
            int b = br.ReadInt32();
            int c = br.ReadInt32();
            int d = br.ReadInt32();

            Guid id = GuidPacker.UnpackGuid(a, b, c, d);
            uint mask = br.ReadUInt32();

            NetworkEntity ne;

            switch (msgType)
            {
                case MessageType.Add:
                    if (networkEntities.TryGetValue(id, out ne))
                    {
                        // Entity exists, update it
                        DecodeMaskedComponents(ne.Local, mask, br);
                    }
                    else
                    {
                        // Create new entity
                        ne = new NetworkEntity(id, playerId, new Entity());

                        // Always add a Transform component!
                        Transform transform = ne.Local.AddComponent(new Transform());
                        if ((mask & (uint)(1 << (int)ComponentType.Transform)) != 0) transform.Decode(br);

                        if ((mask & (uint)(1 << (int)ComponentType.Health)) != 0)
                        {
                            HealthComponent health = ne.Local.AddComponent(new HealthComponent(100));
                            health.Decode(br);
                        }

                        if ((mask & (uint)(1 << (int)ComponentType.BulletHealth)) != 0)
                        {
                            BulletHealthComponent bullet = ne.Local.AddComponent(new BulletHealthComponent(100));
                            bullet.Decode(br);
                        }

                        if ((mask & (uint)(1 << (int)ComponentType.Draw)) != 0)
                        {
                            DrawComponent draw = ne.Local.AddComponent(new DrawComponent(0, Color.White));
                            draw.Decode(br);
                        }

                        networkEntities.Add(id, ne);
                    }
                    break;

                case MessageType.Destroy:
                    networkEntities.TryGetValue(id, out ne);

                    if (ne != null)
                    {
                        ne.Local.Destroy(); // Local entites will be automatically destroyed on next update
                    }

                    networkEntities.Remove(id);
                    break;

                case MessageType.Update:

                    if (networkEntities.TryGetValue(id, out ne))
                    {
                        DecodeMaskedComponents(ne.Local, mask, br);
                        ne.Local.ResetDirtyFlags(mask);
                    }
                    break;
            }
        }

        private static void DecodeMaskedComponents(Entity entity, uint mask, BinaryReader br)
        {
            for (byte i = 0; i <= 5; i++)
            {
                var type = (ComponentType)i;
                if ((mask & (uint)(1 << i)) == 0) continue;

                switch (type)
                {
                    case ComponentType.Transform: entity.GetComponent<Transform>()?.Decode(br); break;
                    case ComponentType.Health: entity.GetComponent<HealthComponent>()?.Decode(br); break;
                    case ComponentType.BulletHealth: entity.GetComponent<BulletHealthComponent>()?.Decode(br); break;
                    case ComponentType.Draw: entity.GetComponent<DrawComponent>()?.Decode(br); break;
                }
            }
        }
    }
}

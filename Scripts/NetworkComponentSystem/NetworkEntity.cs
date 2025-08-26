using System.Numerics;
using Raylib_cs;
using static NetworkComponentSystem.Component;

namespace NetworkComponentSystem
{
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
        internal static byte[] EncodeEntity(NetworkEntity ne, byte MessageType = (byte)2)
        {
            using MemoryStream ms = new MemoryStream();
            using BinaryWriter bw = new BinaryWriter(ms);

            bw.Write(MessageType);                   // MessageType -> 0 Add, 1 Create , 2 Update
            bw.Write(ne.playerId);                   // PlayerId

            var (a, b, c, d) = GuidPacker.PackGuid(ne.id); // EntityId
            bw.Write(a);
            bw.Write(b);
            bw.Write(c);
            bw.Write(d);

            var mask = 0u;
            if (ne.Local.GetComponent<Transform>()?.Dirty ?? false) mask |= (uint)ComponentBits.Transform;
            if (ne.Local.GetComponent<HealthComponent>()?.Dirty ?? false) mask |= (uint)ComponentBits.Health;
            if (ne.Local.GetComponent<MovementComponent>()?.Dirty ?? false) mask |= (uint)ComponentBits.Movement;
            if (ne.Local.GetComponent<BulletHealthComponent>()?.Dirty ?? false) mask |= (uint)ComponentBits.BulletHealth;
            if (ne.Local.GetComponent<DrawComponent>()?.Dirty ?? false) mask |= (uint)ComponentBits.Draw;

            bw.Write(mask);                          // ComponentMask

            // Encode each dirty component *in the order of the mask*
            if ((mask & (uint)ComponentBits.Transform) != 0) Transform.Encode(bw, ne.Local.GetComponent<Transform>());
            if ((mask & (uint)ComponentBits.Health) != 0) HealthComponent.Encode(bw, ne.Local.GetComponent<HealthComponent>());
            if ((mask & (uint)ComponentBits.Movement) != 0) MovementComponent.Encode(bw, ne.Local.GetComponent<MovementComponent>());
            if ((mask & (uint)ComponentBits.BulletHealth) != 0) BulletHealthComponent.Encode(bw, ne.Local.GetComponent<BulletHealthComponent>());
            if ((mask & (uint)ComponentBits.Draw) != 0) DrawComponent.Encode(bw, ne.Local.GetComponent<DrawComponent>());

            // Reset dirty flags
            ne.Local.GetComponent<Transform>()?.ResetDirty();
            ne.Local.GetComponent<HealthComponent>()?.ResetDirty();
            ne.Local.GetComponent<MovementComponent>()?.ResetDirty();
            ne.Local.GetComponent<BulletHealthComponent>()?.ResetDirty();
            ne.Local.GetComponent<DrawComponent>()?.ResetDirty();

            return ms.ToArray();
        }

        public static void ProcessEntity(byte[] data, Dictionary<Guid, NetworkEntity> networkEntities)
        {
            using MemoryStream ms = new MemoryStream(data);
            using BinaryReader br = new BinaryReader(ms);

            byte msgType = br.ReadByte();
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
                case 0:   // Add
                    ne = new NetworkEntity(id, playerId, new Entity());

                    if ((mask & (uint)ComponentBits.Transform) != 0)
                    {
                        Transform transform = ne.Local.AddComponent(new Transform());
                        Transform.Decode(br, transform);
                    }

                    if ((mask & (uint)ComponentBits.Health) != 0)
                    {
                        HealthComponent health = ne.Local.AddComponent(new HealthComponent(100));
                        HealthComponent.Decode(br, health);
                    }

                    if ((mask & (uint)ComponentBits.Movement) != 0)
                    {
                        MovementComponent movement = ne.Local.AddComponent(new MovementComponent(Vector2.Zero, 0f));
                        MovementComponent.Decode(br, movement);
                    }

                    if ((mask & (uint)ComponentBits.BulletHealth) != 0)
                    {
                        BulletHealthComponent bullet = ne.Local.AddComponent(new BulletHealthComponent(100));
                        BulletHealthComponent.Decode(br, bullet);
                    }

                    if ((mask & (uint)ComponentBits.Draw) != 0)
                    {
                        DrawComponent draw = ne.Local.AddComponent(new DrawComponent(0, Color.White));
                        DrawComponent.Decode(br, draw);
                    }

                    networkEntities.Add(id, ne);
                    break;

                case 1:   // Destroy
                    networkEntities.TryGetValue(id, out ne);
                    if (ne != null)
                    {
                        ne.Local.Destroy(); // Local entites will be automatically destroyed on next update
                    }
                    networkEntities.Remove(id);
                    break;

                case 2:   // Update
                    if (!networkEntities.TryGetValue(id, out ne)) return;   // stale packet, ignore

                    // decode each component that is present in the mask
                    if ((mask & (uint)ComponentBits.Transform) != 0) Transform.Decode(br, ne.Local.GetComponent<Transform>());
                    if ((mask & (uint)ComponentBits.Health) != 0) HealthComponent.Decode(br, ne.Local.GetComponent<HealthComponent>());
                    if ((mask & (uint)ComponentBits.Movement) != 0) MovementComponent.Decode(br, ne.Local.GetComponent<MovementComponent>());

                    break;
            }
        }
    }
}

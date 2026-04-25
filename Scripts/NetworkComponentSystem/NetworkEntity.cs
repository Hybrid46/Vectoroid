// ─────────────────────────────────────────────────────────────────────
// NetworkEntity.cs – packet handling
// ─────────────────────────────────────────────────────────────────────
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

            // For Add‑messages we always include all components
            var mask = 0u;

            if (MessageType == 0)          // Add
            {
                if (ne.Local.GetComponent<Transform>() != null) mask |= (uint)ComponentBits.Transform;
                if (ne.Local.GetComponent<HealthComponent>() != null) mask |= (uint)ComponentBits.Health;
                if (ne.Local.GetComponent<MovementComponent>() != null) mask |= (uint)ComponentBits.Movement;
                if (ne.Local.GetComponent<BulletHealthComponent>() != null) mask |= (uint)ComponentBits.BulletHealth;
                if (ne.Local.GetComponent<DrawComponent>() != null) mask |= (uint)ComponentBits.Draw;
            }
            else                            // Update / Destroy
            {
                foreach (Component comp in ne.Local.GetDirtyComponents()) mask |= comp.ComponentMask;
            }
            // ------------------------------------------------------------------

            bw.Write(mask);

            // Encode each dirty component
            if ((mask & (uint)ComponentBits.Transform) != 0) ne.Local.GetComponent<Transform>()?.Encode(bw);
            if ((mask & (uint)ComponentBits.Health) != 0) ne.Local.GetComponent<HealthComponent>()?.Encode(bw);
            if ((mask & (uint)ComponentBits.BulletHealth) != 0) ne.Local.GetComponent<BulletHealthComponent>()?.Encode(bw);
            if ((mask & (uint)ComponentBits.Draw) != 0) ne.Local.GetComponent<DrawComponent>()?.Encode(bw);

            // Reset dirty flags only for Update packets (Add packets are already clean)
            if (MessageType != 0)
            {
                ne.Local.GetComponent<Transform>()?.ResetDirty();
                ne.Local.GetComponent<HealthComponent>()?.ResetDirty();
                ne.Local.GetComponent<MovementComponent>()?.ResetDirty();
                ne.Local.GetComponent<BulletHealthComponent>()?.ResetDirty();
                ne.Local.GetComponent<DrawComponent>()?.ResetDirty();
            }

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
                    if (networkEntities.TryGetValue(id, out ne))
                    {
                        // Entity exists, update it
                        if ((mask & (uint)ComponentBits.Transform) != 0) ne.Local.GetComponent<Transform>()?.Decode(br);
                        if ((mask & (uint)ComponentBits.Health) != 0) ne.Local.GetComponent<HealthComponent>()?.Decode(br);
                        if ((mask & (uint)ComponentBits.BulletHealth) != 0) ne.Local.GetComponent<BulletHealthComponent>()?.Decode(br);
                        if ((mask & (uint)ComponentBits.Draw) != 0) ne.Local.GetComponent<DrawComponent>()?.Decode(br);
                    }
                    else
                    {
                        // Create new entity
                        ne = new NetworkEntity(id, playerId, new Entity());

                        // Always add a Transform component!
                        Transform transform = ne.Local.AddComponent(new Transform());
                        if ((mask & (uint)ComponentBits.Transform) != 0) transform.Decode(br);

                        if ((mask & (uint)ComponentBits.Health) != 0)
                        {
                            HealthComponent health = ne.Local.AddComponent(new HealthComponent(100));
                            health.Decode(br);
                        }

                        if ((mask & (uint)ComponentBits.BulletHealth) != 0)
                        {
                            BulletHealthComponent bullet = ne.Local.AddComponent(new BulletHealthComponent(100));
                            bullet.Decode(br);
                        }

                        if ((mask & (uint)ComponentBits.Draw) != 0)
                        {
                            DrawComponent draw = ne.Local.AddComponent(new DrawComponent(0, Color.White));
                            draw.Decode(br);
                        }

                        networkEntities.Add(id, ne);
                    }
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
                    if ((mask & (uint)ComponentBits.Transform) != 0) ne.Local.GetComponent<Transform>()?.Decode(br);
                    if ((mask & (uint)ComponentBits.Health) != 0 ) ne.Local.GetComponent<HealthComponent>()?.Decode(br);
                    if ((mask & (uint)ComponentBits.BulletHealth) != 0) ne.Local.GetComponent<BulletHealthComponent>()?.Decode(br);
                    if ((mask & (uint)ComponentBits.Draw) != 0 ) ne.Local.GetComponent<DrawComponent>()?.Decode(br);

                    // reset dirty flags after an update
                    ne.Local.GetComponent<Transform>()?.ResetDirty();
                    ne.Local.GetComponent<HealthComponent>()?.ResetDirty();
                    ne.Local.GetComponent<MovementComponent>()?.ResetDirty();
                    ne.Local.GetComponent<BulletHealthComponent>()?.ResetDirty();
                    ne.Local.GetComponent<DrawComponent>()?.ResetDirty();

                    break;
            }
        }
    }
}

using System.Numerics;
using static NetworkComponentSystem.Component;

namespace NetworkComponentSystem
{
    public class NetworkEntity
    {
        // unique per session
        public int id;
        // the *real* entity (server) or the ghost (client)
        public Entity Local;

        public NetworkEntity(int id, Entity local) { 
            this.id = id;
            Local = local;
        }

        //Packet layout -> [MessageType][EntityId][ComponentMask][Payload]
        internal static byte[] EncodeEntity(NetworkEntity ne)
        {
            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms);

            bw.Write((byte)2);                       // MessageType = Update
            bw.Write(ne.id);                         // EntityId

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

        public static void ProcessEntity(byte[] data, Dictionary<int, NetworkEntity> networkEntities)
        {
            using MemoryStream ms = new MemoryStream(data);
            using BinaryReader br = new BinaryReader(ms);

            byte msgType = br.ReadByte();
            int id = br.ReadInt32();
            uint mask = br.ReadUInt32();

            NetworkEntity ne;

            switch (msgType)
            {
                case 0:   // Add
                    ne = new NetworkEntity(id, new Entity());
                    ne.Local.AddComponent(new Transform());
                    ne.Local.AddComponent(new HealthComponent(100));
                    ne.Local.AddComponent(new MovementComponent(Vector2.Zero, 0f));
                    networkEntities.Add(id, ne);
                    break;

                case 1:   // Destroy
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

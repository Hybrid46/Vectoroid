using System.Numerics;
using static NetworkComponentSystem.Component;

namespace NetworkComponentSystem
{
    internal class NetworkEntity
    {
        // unique per session
        public int Id;
        // the *real* entity (server) or the ghost (client)
        public Entity Local;

        public NetworkEntity(int id, Entity local) { Id = id; Local = local; }

        internal static byte[] EncodeEntity(NetworkEntity ne)
        {
            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms);

            bw.Write((byte)2);                       // MessageType = Update
            bw.Write(ne.Id);                         // EntityId

            var mask = 0u;
            if (ne.Local.GetComponent<Transform>()?.Dirty ?? false) mask |= (uint)ComponentBits.Transform;
            if (ne.Local.GetComponent<HealthComponent>()?.Dirty ?? false) mask |= (uint)ComponentBits.Health;
            if (ne.Local.GetComponent<MovementComponent>()?.Dirty ?? false) mask |= (uint)ComponentBits.Movement;
            if (ne.Local.GetComponent<ColorComponent>()?.Dirty ?? false) mask |= (uint)ComponentBits.Color;

            bw.Write(mask);                          // ComponentMask

            // Encode each dirty component *in the order of the mask*
            if ((mask & (uint)ComponentBits.Transform) != 0) Transform.Encode(bw, ne.Local.GetComponent<Transform>());
            if ((mask & (uint)ComponentBits.Health) != 0) HealthComponent.Encode(bw, ne.Local.GetComponent<HealthComponent>());
            if ((mask & (uint)ComponentBits.Movement) != 0) MovementComponent.Encode(bw, ne.Local.GetComponent<MovementComponent>());
            if ((mask & (uint)ComponentBits.Color) != 0) ColorComponent.Encode(bw, ne.Local.GetComponent<ColorComponent>());

            // Reset dirty flags
            ne.Local.GetComponent<Transform>()?.ResetDirty();
            ne.Local.GetComponent<HealthComponent>()?.ResetDirty();
            ne.Local.GetComponent<MovementComponent>()?.ResetDirty();
            ne.Local.GetComponent<ColorComponent>()?.ResetDirty();

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

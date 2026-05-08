namespace NetworkComponentSystem
{
    public abstract class Component
    {
        public enum ComponentType : byte
        {
            Transform = 0,
            Health = 1,
            Movement = 2,
            BulletHealth = 3,
            Draw = 4,
            Controller = 5,
        }

        public ComponentType componentType { get; protected set; }
        public uint ComponentMask => (uint)(1 << (int)componentType);

        public Entity Entity { get; set; }
        public bool Dirty { get; set; } = true;

        public void MarkDirty() => Dirty = true;
        public void ResetDirty() => Dirty = false;
    }
}
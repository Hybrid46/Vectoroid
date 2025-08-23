namespace NetworkComponentSystem
{
    public abstract class Component
    {
        [Flags]
        public enum ComponentBits
        {
            Transform = 1 << 0,
            Health = 1 << 1,
            Movement = 1 << 2,
            Color = 1 << 3,
            BulletHealth = 1 << 4
        }

        public Entity Entity { get; set; }
        public bool Dirty { get; set; } = true;
        public void MarkDirty() => Dirty = true;
        public void ResetDirty() => Dirty = false;
    }
}

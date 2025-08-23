namespace NetworkComponentSystem
{
    public abstract class Component
    {
        public Entity Entity { get; set; }
        public bool Dirty { get; set; } = true;
        public void MarkDirty() => Dirty = true;
    }
}

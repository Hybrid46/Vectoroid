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
            BulletHealth = 1 << 3,
            Draw = 1 << 4,
            Controller = 1 << 5,
        }

        public Entity Entity { get; set; }
        public bool Dirty { get; set; } = true;

        public static ComponentBits ToBits<T>() where T : Component
        {
            if (typeof(T) == typeof(Transform)) return ComponentBits.Transform;
            if (typeof(T) == typeof(HealthComponent)) return ComponentBits.Health;
            if (typeof(T) == typeof(MovementComponent)) return ComponentBits.Movement;
            if (typeof(T) == typeof(BulletHealthComponent)) return ComponentBits.BulletHealth;
            if (typeof(T) == typeof(DrawComponent)) return ComponentBits.Draw;
            if (typeof(T) == typeof(ControllerComponent)) return ComponentBits.Controller;
            return ComponentBits.Transform;
        }

        public void MarkDirty() => Dirty = true;
        public void ResetDirty() => Dirty = false;
    }
}
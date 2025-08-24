namespace NetworkComponentSystem
{
    public class Entity
    {
        private readonly Dictionary<Type, Component> _components = new();
        private Transform _cachedTransform;
        private HealthComponent _cachedHealthComponent;
        private MovementComponent _cachedMovementComponent;
        private DrawComponent _cachedDrawComponent;
        private ControllerComponent _cachedControllerComponent;

        public bool destroy { get; private set; } = false;

        public Transform transform => _cachedTransform;
        public HealthComponent healthComponent => _cachedHealthComponent;
        public MovementComponent movementComponent => _cachedMovementComponent;
        public DrawComponent drawComponent => _cachedDrawComponent;
        public ControllerComponent controllerComponent => _cachedControllerComponent;

        public Entity()
        {
            AddComponent(new Transform());
            Console.WriteLine($"Entity {GetHashCode()} created");
        }

        public T AddComponent<T>(T component) where T : Component
        {
            component.Entity = this;
            _components[typeof(T)] = component;

            // Update cache
            if (component is Transform transform) _cachedTransform = transform;
            if (component is HealthComponent healthComponent) _cachedHealthComponent = healthComponent;
            if (component is BulletHealthComponent bulletHealthComponent) _cachedHealthComponent = bulletHealthComponent;
            if (component is MovementComponent movement) _cachedMovementComponent = movement;
            if (component is DrawComponent draw) _cachedDrawComponent = draw;
            if (component is ControllerComponent control) _cachedControllerComponent = control;

            return component;
        }

        public T GetComponent<T>() where T : Component
        {
            if (typeof(T) == typeof(Transform)) return _cachedTransform as T;
            if (typeof(T) == typeof(HealthComponent)) return _cachedHealthComponent as T;
            if (typeof(T) == typeof(BulletHealthComponent)) return _cachedHealthComponent as T;
            if (typeof(T) == typeof(MovementComponent)) return _cachedMovementComponent as T;
            if (typeof(T) == typeof(DrawComponent)) return _cachedDrawComponent as T;
            if (typeof(T) == typeof(ControllerComponent)) return _cachedControllerComponent as T;

            return _components.TryGetValue(typeof(T), out var component) ? (T)component : null;
        }

        public void Update()
        {
            // Update all updatable components
            foreach (var component in _components.Values.OfType<IUpdatable>())
            {
                component.Update();
            }
        }

        public void Destroy()
        {
            destroy = true;
            Console.WriteLine($"Entity {GetHashCode()} destroyed");
        }
    }
}

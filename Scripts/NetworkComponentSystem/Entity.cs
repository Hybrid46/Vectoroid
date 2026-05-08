using static NetworkComponentSystem.Component;

namespace NetworkComponentSystem
{
    public class Entity
    {
        private readonly Dictionary<Type, Component> _components = new();
        private readonly Dictionary<Type, Component> _networkComponents = new();

        public bool destroy { get; private set; } = false;

        public Transform transform => GetComponent<Transform>();
        public HealthComponent healthComponent => GetComponent<HealthComponent>();
        public MovementComponent movementComponent => GetComponent<MovementComponent>();
        public DrawComponent drawComponent => GetComponent<DrawComponent>();
        public ControllerComponent controllerComponent => GetComponent<ControllerComponent>();

        public Entity()
        {
            AddComponent(new Transform());
            Console.WriteLine($"Entity {GetHashCode()} created");
        }

        public IEnumerable<INetworkComponent> GetDirtyNetworkComponents()
        {
            foreach (Component c in _networkComponents.Values)
            {
                if (c.Dirty) yield return (INetworkComponent)c;
            }
        }

        public T AddComponent<T>(T component) where T : Component
        {
            component.Entity = this;
            _components[typeof(T)] = component;
            if (component is INetworkComponent) _networkComponents[typeof(T)] = component;

            return component;
        }

        //TODO Remove component

        public T GetComponent<T>() where T : Component
        {
            return _components.TryGetValue(typeof(T), out var component) ? (T)component : null;
        }

        public Component GetComponentByType(ComponentType type)
        {
            return _components.Values.FirstOrDefault(c => c.componentType == type);
        }

        public bool HasComponent<T>() where T : Component => _components.ContainsKey(typeof(T));

        public bool HasDirtyComponent()
        {
            foreach (var component in _networkComponents.Values)
            {
                if (component.Dirty) return true;
            }

            return false;
        }

        public uint GetFullNetworkMask()
        {
            uint mask = 0;

            foreach (var comp in _networkComponents.Values)
            {
                mask |= comp.ComponentMask;
            }

            return mask;
        }

        public uint GetDirtyNetworkMask()
        {
            uint mask = 0;

            foreach (var comp in _networkComponents.Values)
            {
                if (comp.Dirty) mask |= comp.ComponentMask;
            }

            return mask;
        }

        public void ResetDirtyFlags(uint mask)
        {
            foreach (var comp in _networkComponents.Values)
            {
                if ((mask & comp.ComponentMask) != 0) comp.ResetDirty();
            }
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

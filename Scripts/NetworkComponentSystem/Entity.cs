using System.Numerics;
using static NetworkComponentSystem.Component;

namespace NetworkComponentSystem
{
    public class Entity
    {
        private readonly Dictionary<Type, Component> _components = new();

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

        public IEnumerable<Component> GetDirtyComponents()
        {
            foreach (Component c in _components.Values)
            {
                if (c.Dirty && c is INetworkComponent) yield return c;
            }
        }

        public T AddComponent<T>(T component) where T : Component
        {
            component.Entity = this;
            _components[typeof(T)] = component;

            return component;
        }

        public T GetComponent<T>() where T : Component
        {
            return _components.TryGetValue(typeof(T), out var component) ? (T)component : null;
        }

        public bool HasComponent<T>() where T : Component => _components.ContainsKey(typeof(T));

        public bool HasDirtyComponent()
        {
            foreach (var component in _components.Values)
            {
                if (component.Dirty && component is INetworkComponent) return true;
            }

            return false;
        }

        public uint GetDirtyMask()
        {
            uint mask = 0;

            foreach (var comp in _components.Values)
            {
                if (comp.Dirty && comp is INetworkComponent) mask |= comp.ComponentMask;
            }

            return mask;
        }

        public void ResetDirtyFlags(uint mask)
        {
            foreach (var comp in _components.Values)
            {
                if ((mask & comp.ComponentMask) != 0 && comp is INetworkComponent) comp.ResetDirty();
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

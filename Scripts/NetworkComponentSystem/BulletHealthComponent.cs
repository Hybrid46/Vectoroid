namespace NetworkComponentSystem
{
        public class BulletHealthComponent : HealthComponent, IUpdatable
        {
            public BulletHealthComponent(int maxHP) : base(maxHP)
            {
                componentType = ComponentType.BulletHealth;
            }

            public void Update()
            {
                TakeDamage(1); // Damage over time
            }

            protected override void Die()
            {
                Entity.Destroy();
                Console.WriteLine("Bullet destroyed!");
            }

            public void Encode(BinaryWriter bw)
            {
                base.Encode(bw);
            }

            public void Decode(BinaryReader br)
            {
                base.Decode(br);
            }
        }
    }

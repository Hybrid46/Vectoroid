namespace NetworkComponentSystem
{
    public class BulletHealthComponent : HealthComponent, IUpdatable
    {
        public BulletHealthComponent(int maxHP) : base(maxHP) { }

        public void Update()
        {
            TakeDamage(1); // Damage over time
        }

        protected override void Die()
        {
            Entity.Destroy();
            Console.WriteLine("Bullet destroyed!");
        }
    }
}

namespace NetworkComponentSystem
{
    public class HealthComponent : Component
    {
        private int _maxHP;
        private int _currentHP;

        public int MaxHP
        {
            get => _maxHP;
            set
            {
                if (_maxHP != value)
                {
                    _maxHP = value;
                    MarkDirty();
                }
            }
        }

        public int CurrentHP
        {
            get => _currentHP;
            set
            {;
                if (_currentHP != value)
                {
                    _currentHP = value;
                    MarkDirty();
                }
            }
        }

        public HealthComponent(int maxHP)
        {
            _maxHP = maxHP;
            _currentHP = maxHP;
        }

        public void TakeDamage(int amount)
        {
            CurrentHP = Math.Clamp(CurrentHP - amount, 0, MaxHP);
            if (CurrentHP == 0) Die();
        }

        protected virtual void Die()
        {
            Entity.Destroy();
            MarkDirty();
            Console.WriteLine($"Entity {GetHashCode()} died!");
        }
    }
}

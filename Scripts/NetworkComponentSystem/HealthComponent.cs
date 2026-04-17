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
            MaxHP = maxHP;
            CurrentHP = maxHP;
        }

        public void TakeDamage(int amount)
        {
            CurrentHP = Math.Clamp(CurrentHP - amount, 0, MaxHP);
            MarkDirty();
            if (CurrentHP == 0) Die();
        }

        protected virtual void Die()
        {
            Entity.Destroy();
            MarkDirty();
            Console.WriteLine($"Entity {GetHashCode()} died!");
        }

        public static void Encode(BinaryWriter bw, HealthComponent h)
        {
            bw.Write(h.CurrentHP);
            bw.Write(h.MaxHP);
        }

        public static void Decode(BinaryReader br, HealthComponent h)
        {
            h.CurrentHP = br.ReadInt32();
            h.MaxHP = br.ReadInt32();
        }
    }
}
